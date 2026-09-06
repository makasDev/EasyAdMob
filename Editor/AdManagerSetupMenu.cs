#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EasyAdMob.Editor
{
    public class AdManagerSetupMenu : EditorWindow
    {
        private const string ADMOB_PACKAGE_URL = "https://github.com/googleads/googleads-mobile-unity/releases/download/v11.4.0/GoogleMobileAds-v11.4.0.unitypackage";
        private const string TEMP_FILE_PATH = "Temp/GoogleMobileAds.unitypackage";
        private const string SCRIPTING_DEFINE_SYMBOL = "EASY_ADMOB_GOOGLE_MOBILE_ADS";

        private const string TEST_ANDROID_APP_ID = "ca-app-pub-3940256099942544~3347511713";
        private const string TEST_IOS_APP_ID = "ca-app-pub-3940256099942544~1458002511";
        private const string TEST_BANNER_ID = "ca-app-pub-3940256099942544/6300978111";
        private const string TEST_INTERSTITIAL_ID = "ca-app-pub-3940256099942544/1033173712";
        private const string TEST_REWARDED_ID = "ca-app-pub-3940256099942544/5224354917";
        private const string TEST_REWARDED_INTERSTITIAL_ID = "ca-app-pub-3940256099942544/5354046379";

        // EditorPrefs keys — namespaced per-project via PlayerSettings.productGUID so
        // multiple projects on the same machine don't stomp on each other's saved IDs.
        private const string PREFS_PREFIX = "EasyAdMob.SetupMenu.";

        // App IDs use a tilde between the publisher block and the app number:
        //   ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY
        // Ad unit IDs use a forward slash instead:
        //   ca-app-pub-XXXXXXXXXXXXXXXX/YYYYYYYYYY
        // The digit counts (16 and 10) match every real and test ID Google has published,
        // so this is a safe, tight check without being fragile to minor future variance.
        private static readonly Regex AppIdPattern = new Regex(@"^ca-app-pub-\d{16}~\d{10}$", RegexOptions.Compiled);
        private static readonly Regex AdUnitIdPattern = new Regex(@"^ca-app-pub-\d{16}/\d{10}$", RegexOptions.Compiled);

        private enum IdKind { AppId, AdUnitId }

        private static bool IsValidId(string value, IdKind kind)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return kind == IdKind.AppId
                ? AppIdPattern.IsMatch(value.Trim())
                : AdUnitIdPattern.IsMatch(value.Trim());
        }

        private bool isDownloading = false;
        private float downloadProgress = 0f;

        // Each field now has a paired "is this a custom (user-entered) value?" flag.
        // When false, the field is still showing the test id and is rendered as a
        // grayed-out placeholder; when true, it's the user's real id and renders normally.
        private string androidAppId = TEST_ANDROID_APP_ID;
        private bool androidAppIdIsCustom = false;

        private string iosAppId = TEST_IOS_APP_ID;
        private bool iosAppIdIsCustom = false;

        private string bannerId = TEST_BANNER_ID;
        private bool bannerIdIsCustom = false;

        private string interstitialId = TEST_INTERSTITIAL_ID;
        private bool interstitialIdIsCustom = false;

        private string rewardedId = TEST_REWARDED_ID;
        private bool rewardedIdIsCustom = false;

        private string rewardedInterstitialId = TEST_REWARDED_INTERSTITIAL_ID;
        private bool rewardedInterstitialIdIsCustom = false;

        [MenuItem("Tools/EasyAdMob/Setup Wizard", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdManagerSetupMenu>("EasyAdMob Setup");
            window.minSize = new Vector2(440, 760);
            window.Show();
        }

        private void OnEnable()
        {
            LoadPersistedIds();
        }

        private void OnDisable()
        {
            // Defensive flush: guarantees whatever is currently in the in-memory fields is
            // written out when the window closes, loses focus during an assembly reload, or
            // Unity itself is closing - so a save is never dependent purely on the last
            // keystroke's EndChangeCheck() having fired.
            SavePersistedIds();
        }

        private void OnGUI()
        {
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150f;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("EasyAdMob Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use this wizard to install dependencies, configure App/Ad IDs, and setup AdManager in your scene.", MessageType.Info);
            
            GUILayout.Space(10);

            bool hasAdMobAssembly = CheckAdMobInstalled();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step 1: Dependencies & Symbols", EditorStyles.boldLabel);
            
            if (hasAdMobAssembly)
            {
                EditorGUILayout.HelpBox("Google Mobile Ads SDK is installed!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Google Mobile Ads SDK not detected.", MessageType.Warning);
                
                GUI.enabled = !isDownloading;
                if (GUILayout.Button(isDownloading ? $"Downloading ({downloadProgress * 100:F0}%)..." : "Download & Install Google Mobile Ads", GUILayout.Height(30)))
                {
                    DownloadAndInstallAdMob();
                }
                GUI.enabled = true;
            }

            bool hasDefine = HasScriptingDefineSymbol();
            if (!hasDefine && GUILayout.Button("Add Scripting Define Symbol", GUILayout.Height(30)))
            {
                AddScriptingDefineSymbol();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step 2: Android SDK Architecture", EditorStyles.boldLabel);
            GUILayout.Space(4);

            bool? isNextGen = IsNextGenAndroidSdkActive();

            if (isNextGen == null)
            {
                EditorGUILayout.HelpBox("Could not read the current architecture from GoogleMobileAdsSettings. Install/select the Google Mobile Ads SDK first.", MessageType.Warning);
            }
            else if (isNextGen.Value)
            {
                EditorGUILayout.HelpBox("Currently using the GMA Next-Gen SDK.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Currently using the standard Google Mobile Ads SDK. The Next-Gen SDK improves latency and stability, and requires min API level 24+.", MessageType.Info);

                if (GUILayout.Button("Switch to Next-Gen Android SDK", GUILayout.Height(30)))
                {
                    Type googleSettingsType = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor")
                                           ?? Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Core.Editor");
                    UnityEngine.Object settingsInstance = googleSettingsType != null ? Resources.Load("GoogleMobileAdsSettings") : null;

                    if (settingsInstance == null || googleSettingsType == null)
                    {
                        EditorUtility.DisplayDialog("GoogleMobileAdsSettings Not Found", "Open 'Assets > Google Mobile Ads > Settings' once to create the settings asset, then try again.", "OK");
                    }
                    else
                    {
                        bool confirmSwitch = EditorUtility.DisplayDialog(
                            "Switch to Next-Gen Android SDK?",
                            "This switches your project's Google Mobile Ads Android architecture to the Next-Gen SDK. This requires minimum API level 24+. You can switch back to the standard SDK at any time from Assets > Google Mobile Ads > Settings.",
                            "Switch",
                            "Cancel");

                        if (confirmSwitch)
                        {
                            TrySwitchToNextGenAndroidSdk(settingsInstance, googleSettingsType);
                        }
                    }
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step 3: App & Ad Unit IDs", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.LabelField("Google Mobile Ads App IDs", EditorStyles.miniBoldLabel);
            androidAppId = DrawIdField("Android App ID", androidAppId, TEST_ANDROID_APP_ID, IdKind.AppId, ref androidAppIdIsCustom);
            iosAppId = DrawIdField("iOS App ID", iosAppId, TEST_IOS_APP_ID, IdKind.AppId, ref iosAppIdIsCustom);

            GUILayout.Space(6);
            EditorGUILayout.LabelField("Ad Unit IDs", EditorStyles.miniBoldLabel);
            bannerId = DrawIdField("Banner ID", bannerId, TEST_BANNER_ID, IdKind.AdUnitId, ref bannerIdIsCustom);
            interstitialId = DrawIdField("Interstitial ID", interstitialId, TEST_INTERSTITIAL_ID, IdKind.AdUnitId, ref interstitialIdIsCustom);
            rewardedId = DrawIdField("Rewarded ID", rewardedId, TEST_REWARDED_ID, IdKind.AdUnitId, ref rewardedIdIsCustom);
            rewardedInterstitialId = DrawIdField("Rewarded Interstitial ID", rewardedInterstitialId, TEST_REWARDED_INTERSTITIAL_ID, IdKind.AdUnitId, ref rewardedInterstitialIdIsCustom);

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill Test IDs", GUILayout.Height(30)))
            {
                TryFillTestIDs();
            }

            if (GUILayout.Button("Apply IDs to Project & Scene", GUILayout.Height(30)))
            {
                TryApplyIDsToProjectAndScene();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step 4: Scene Setup", EditorStyles.boldLabel);
            GUILayout.Space(4);

            if (GUILayout.Button("Create [AdManager] in Active Scene", GUILayout.Height(30)))
            {
                CreateAdManagerInScene();
                TryApplyIDsToProjectAndScene();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Generate Showcase Demo Scene", GUILayout.Height(30)))
            {
                EditorApplication.delayCall += () => GenerateShowcaseScene();
            }
            EditorGUILayout.EndVertical();

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        /// <summary>
        /// Draws a single ID field with test/custom visual treatment.
        /// - If the field currently holds the test value (isCustom == false), it's drawn
        ///   grayed out with a "(Test ID)" suffix label, mimicking a placeholder.
        /// - The moment the user types a value different from the test id, isCustom flips
        ///   to true, the field renders as a normal active text box, and the value persists.
        /// - If the user clears the field back to empty or back to the exact test value,
        ///   it flips back to the test/placeholder state.
        /// </summary>
        private static GUIStyle placeholderFieldStyle;

        private static GUIStyle GetPlaceholderFieldStyle()
        {
            // Lazily built so it picks up the active skin's textField as a base (dark/light skin safe).
            if (placeholderFieldStyle == null)
            {
                placeholderFieldStyle = new GUIStyle(EditorStyles.textField);
                Color dim = EditorGUIUtility.isProSkin
                    ? new Color(0.6f, 0.6f, 0.6f)
                    : new Color(0.45f, 0.45f, 0.45f);
                placeholderFieldStyle.normal.textColor = dim;
                placeholderFieldStyle.focused.textColor = dim;
            }
            return placeholderFieldStyle;
        }

        private static GUIStyle invalidFieldStyle;

        private static GUIStyle GetInvalidFieldStyle()
        {
            if (invalidFieldStyle == null)
            {
                invalidFieldStyle = new GUIStyle(EditorStyles.textField);
                Color warn = new Color(1f, 0.45f, 0.4f); // soft red, readable on both skins
                invalidFieldStyle.normal.textColor = warn;
                invalidFieldStyle.focused.textColor = warn;
            }
            return invalidFieldStyle;
        }

        private string DrawIdField(string label, string currentValue, string testValue, IdKind kind, ref bool isCustom)
        {
            bool currentIsValid = !isCustom || IsValidId(currentValue, kind);

            EditorGUILayout.BeginHorizontal();

            // Field stays editable either way - only the text color changes so it reads
            // like a placeholder (dimmed) or a bad value (red) without blocking input.
            GUIStyle style;
            if (!isCustom)
            {
                style = GetPlaceholderFieldStyle();
            }
            else if (!currentIsValid)
            {
                style = GetInvalidFieldStyle();
            }
            else
            {
                style = EditorStyles.textField;
            }

            // BeginChangeCheck/EndChangeCheck is the correct way to detect "the user actually
            // changed this control" in IMGUI. Comparing the returned string to the old one
            // directly is unreliable here: OnGUI runs multiple passes per frame (Layout,
            // Repaint, mouse-move repaints, etc.) and can re-enter with stale arguments,
            // so a plain "newValue != currentValue" check can both mis-fire and, worse,
            // silently no-op on real edits depending on which pass happens to call this.
            // That mismatch is what was making saving feel inconsistent.
            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUILayout.TextField(label, currentValue, style);
            bool changed = EditorGUI.EndChangeCheck();

            if (!isCustom)
            {
                GUILayout.Label("(Test ID)", EditorStyles.miniLabel, GUILayout.Width(60));
            }
            else
            {
                GUILayout.Space(64); // keep field width consistent whether or not the tag is shown
            }

            EditorGUILayout.EndHorizontal();

            if (isCustom && !currentIsValid)
            {
                string expected = kind == IdKind.AppId
                    ? "ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY"
                    : "ca-app-pub-XXXXXXXXXXXXXXXX/YYYYYYYYYY";
                EditorGUILayout.HelpBox($"Doesn't look like a valid {(kind == IdKind.AppId ? "App ID" : "Ad Unit ID")}. Expected format: {expected}", MessageType.Warning);
            }

            if (!changed)
            {
                return currentValue;
            }

            if (string.IsNullOrEmpty(newValue) || newValue == testValue)
            {
                // user cleared it or retyped the test id manually -> revert to test/placeholder state
                isCustom = false;
                SavePersistedIds();
                return testValue;
            }

            isCustom = true;
            SavePersistedIds();
            return newValue;
        }

        private void TryFillTestIDs()
        {
            bool anyCustom = androidAppIdIsCustom || iosAppIdIsCustom || bannerIdIsCustom
                || interstitialIdIsCustom || rewardedIdIsCustom || rewardedInterstitialIdIsCustom;

            if (anyCustom)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Overwrite Custom Ad IDs?",
                    "You have custom Ad/App IDs entered. Filling test IDs will overwrite them. This cannot be undone from within the wizard.",
                    "Overwrite with Test IDs",
                    "Cancel");

                if (!confirmed)
                {
                    return;
                }
            }

            FillTestIDs();
        }

        private void FillTestIDs()
        {
            androidAppId = TEST_ANDROID_APP_ID;
            androidAppIdIsCustom = false;

            iosAppId = TEST_IOS_APP_ID;
            iosAppIdIsCustom = false;

            bannerId = TEST_BANNER_ID;
            bannerIdIsCustom = false;

            interstitialId = TEST_INTERSTITIAL_ID;
            interstitialIdIsCustom = false;

            rewardedId = TEST_REWARDED_ID;
            rewardedIdIsCustom = false;

            rewardedInterstitialId = TEST_REWARDED_INTERSTITIAL_ID;
            rewardedInterstitialIdIsCustom = false;

            SavePersistedIds();
            Debug.Log("[EasyAdMob] Filled fields with default AdMob Test IDs.");
        }

        // ===================== PERSISTENCE =====================

        private static string ProjectKey => PlayerSettings.productGUID.ToString();

        private static string PrefKey(string field) => $"{PREFS_PREFIX}{ProjectKey}.{field}";

        private void LoadPersistedIds()
        {
            LoadField("androidAppId", TEST_ANDROID_APP_ID, out androidAppId, out androidAppIdIsCustom);
            LoadField("iosAppId", TEST_IOS_APP_ID, out iosAppId, out iosAppIdIsCustom);
            LoadField("bannerId", TEST_BANNER_ID, out bannerId, out bannerIdIsCustom);
            LoadField("interstitialId", TEST_INTERSTITIAL_ID, out interstitialId, out interstitialIdIsCustom);
            LoadField("rewardedId", TEST_REWARDED_ID, out rewardedId, out rewardedIdIsCustom);
            LoadField("rewardedInterstitialId", TEST_REWARDED_INTERSTITIAL_ID, out rewardedInterstitialId, out rewardedInterstitialIdIsCustom);
        }

        private void LoadField(string fieldName, string testValue, out string value, out bool isCustom)
        {
            string customKey = PrefKey(fieldName);
            string savedValue = EditorPrefs.GetString(customKey, string.Empty);

            if (!string.IsNullOrEmpty(savedValue) && savedValue != testValue)
            {
                value = savedValue;
                isCustom = true;
            }
            else
            {
                value = testValue;
                isCustom = false;
            }
        }

        private void SavePersistedIds()
        {
            SaveField("androidAppId", androidAppId, androidAppIdIsCustom);
            SaveField("iosAppId", iosAppId, iosAppIdIsCustom);
            SaveField("bannerId", bannerId, bannerIdIsCustom);
            SaveField("interstitialId", interstitialId, interstitialIdIsCustom);
            SaveField("rewardedId", rewardedId, rewardedIdIsCustom);
            SaveField("rewardedInterstitialId", rewardedInterstitialId, rewardedInterstitialIdIsCustom);
        }

        private void SaveField(string fieldName, string value, bool isCustom)
        {
            string key = PrefKey(fieldName);
            if (isCustom)
            {
                EditorPrefs.SetString(key, value);
            }
            else
            {
                EditorPrefs.DeleteKey(key);
            }
        }

        private bool CheckAdMobInstalled()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "GoogleMobileAds.Core" || a.GetName().Name == "GoogleMobileAds");
        }

        private void DownloadAndInstallAdMob()
        {
            isDownloading = true;
            downloadProgress = 0f;

            UnityWebRequest request = UnityWebRequest.Get(ADMOB_PACKAGE_URL);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            EditorApplication.CallbackFunction updateProgress = null;
            updateProgress = () =>
            {
                if (!operation.isDone)
                {
                    downloadProgress = operation.progress;
                    Repaint();
                }
                else
                {
                    EditorApplication.update -= updateProgress;
                    isDownloading = false;

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[EasyAdMob] Failed to download SDK: {request.error}");
                        EditorUtility.DisplayDialog("Download Error", request.error, "OK");
                    }
                    else
                    {
                        File.WriteAllBytes(TEMP_FILE_PATH, request.downloadHandler.data);
                        Debug.Log("[EasyAdMob] Download finished. Unpacking package...");
                        
                        AddScriptingDefineSymbol();
                        AssetDatabase.ImportPackage(TEMP_FILE_PATH, interactive: true);
                    }
                    Repaint();
                }
            };

            EditorApplication.update += updateProgress;
        }

        /// <summary>
        /// Validates every field before writing anything to the project/scene. Test-id fields
        /// are always valid (they're Google's own values), so this only ever blocks on a
        /// custom value the user typed wrong. Shows exactly which field(s) are bad and lets
        /// them choose to fix it first or apply anyway (in case the format check is ever
        /// wrong about some new, valid AdMob id shape).
        /// </summary>
        private void TryApplyIDsToProjectAndScene()
        {
            var invalidFields = new System.Collections.Generic.List<string>();

            if (androidAppIdIsCustom && !IsValidId(androidAppId, IdKind.AppId)) invalidFields.Add("Android App ID");
            if (iosAppIdIsCustom && !IsValidId(iosAppId, IdKind.AppId)) invalidFields.Add("iOS App ID");
            if (bannerIdIsCustom && !IsValidId(bannerId, IdKind.AdUnitId)) invalidFields.Add("Banner ID");
            if (interstitialIdIsCustom && !IsValidId(interstitialId, IdKind.AdUnitId)) invalidFields.Add("Interstitial ID");
            if (rewardedIdIsCustom && !IsValidId(rewardedId, IdKind.AdUnitId)) invalidFields.Add("Rewarded ID");
            if (rewardedInterstitialIdIsCustom && !IsValidId(rewardedInterstitialId, IdKind.AdUnitId)) invalidFields.Add("Rewarded Interstitial ID");

            if (invalidFields.Count > 0)
            {
                string fieldList = string.Join("\n- ", invalidFields);
                bool applyAnyway = EditorUtility.DisplayDialog(
                    "Invalid Ad/App ID Format",
                    $"The following field(s) don't match the expected AdMob ID format:\n\n- {fieldList}\n\nApplying anyway may break ad loading at runtime.",
                    "Apply Anyway",
                    "Cancel and Fix");

                if (!applyAnyway)
                {
                    return;
                }
            }

            ApplyIDsToProjectAndScene();
        }

        private void ApplyIDsToProjectAndScene()
        {
            Type googleSettingsType = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor")
                                   ?? Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Core.Editor");

            if (googleSettingsType != null)
            {
                UnityEngine.Object settingsInstance = Resources.Load("GoogleMobileAdsSettings");

                if (settingsInstance == null)
                {
                    PropertyInfo instanceProp = googleSettingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    settingsInstance = instanceProp?.GetValue(null) as UnityEngine.Object;
                }

                if (settingsInstance != null)
                {
                    SerializedObject serializedSettings = new SerializedObject(settingsInstance);
                    
                    SerializedProperty adMobAndroidAppIdProp = serializedSettings.FindProperty("adMobAndroidAppId");
                    SerializedProperty adMobIOSAppIdProp = serializedSettings.FindProperty("adMobIOSAppId");

                    if (adMobAndroidAppIdProp != null && !string.IsNullOrEmpty(androidAppId))
                    {
                        adMobAndroidAppIdProp.stringValue = androidAppId.Trim();
                    }

                    if (adMobIOSAppIdProp != null && !string.IsNullOrEmpty(iosAppId))
                    {
                        adMobIOSAppIdProp.stringValue = iosAppId.Trim();
                    }

                    // Gradle build pre-processor / kotlinx.coroutines packaging: plain bools,
                    // safe to force on directly.
                    SetSerializedBool(serializedSettings, "enableGradleBuildPreProcessor", true);
                    SetSerializedBool(serializedSettings, "enableKotlinXCoroutinesPackagingOption", true);

                    serializedSettings.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settingsInstance);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[EasyAdMob] Successfully updated Google Mobile Ads App IDs in Settings!");
                }
                else
                {
                    Debug.LogWarning("[EasyAdMob] Could not find or instantiate GoogleMobileAdsSettings asset. Open 'Assets > Google Mobile Ads > Settings' once in Unity to create it.");
                }
            }

            Type adManagerType = Type.GetType("EasyAdMob.AdManager, EasyAdMob.Runtime");
            if (adManagerType != null)
            {
                ApplyAdUnitIdsAcrossAllScenes(adManagerType);
            }
        }

        /// <summary>
        /// Finds every [AdManager] across every scene registered in Build Settings and applies
        /// the current Ad Unit IDs to each one - not just whatever scene happens to be open.
        ///
        /// Scenes not in Build Settings are intentionally skipped: those won't ship in the
        /// build anyway, and scanning literally every .unity file under Assets/ (including old
        /// test scenes, backups, etc.) risks touching things the user never intended to touch.
        /// If a scene needs its AdManager updated, it needs to be in Build Settings first.
        ///
        /// The currently open scene(s) and their dirty state are preserved: any scene opened
        /// here purely to apply IDs is opened additively and closed again afterward, and the
        /// scene the user started in is never force-saved.
        /// </summary>
        private void ApplyAdUnitIdsAcrossAllScenes(Type adManagerType)
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes == null || buildScenes.Length == 0)
            {
                Debug.LogWarning("[EasyAdMob] No scenes found in Build Settings. Add your scenes there so the wizard knows which ones to update.");
                return;
            }

            // Remember what was open before we start, so we can restore it exactly afterward.
            string originalSetupPath = EditorSceneManager.GetActiveScene().path;
            var originallyOpenScenePaths = new System.Collections.Generic.List<string>();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var openScene = EditorSceneManager.GetSceneAt(i);
                if (openScene.isLoaded)
                {
                    originallyOpenScenePaths.Add(openScene.path);
                }
            }

            // If anything in the currently open scene(s) is unsaved, ask first - opening/closing
            // other scenes additively is safe, but we don't want to surprise the user by
            // touching scene state while they have unrelated unsaved edits sitting around.
            if (EditorSceneManager.GetActiveScene().isDirty || AnyOpenSceneIsDirty())
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Unsaved Scene Changes",
                    "You have unsaved changes in your currently open scene(s). Applying IDs across all scenes will open and close other scenes in the editor. Save your current scene(s) first to avoid confusion.",
                    "Continue Anyway",
                    "Cancel");

                if (!proceed)
                {
                    return;
                }
            }

            int updatedCount = 0;
            int scannedCount = 0;

            foreach (EditorBuildSettingsScene buildScene in buildScenes)
            {
                if (buildScene == null || string.IsNullOrEmpty(buildScene.path) || !buildScene.enabled)
                {
                    continue;
                }

                if (!File.Exists(buildScene.path))
                {
                    Debug.LogWarning($"[EasyAdMob] Skipped missing scene: {buildScene.path}");
                    continue;
                }

                bool wasAlreadyOpen = originallyOpenScenePaths.Contains(buildScene.path);
                Scene scene;

                if (wasAlreadyOpen)
                {
                    scene = EditorSceneManager.GetSceneByPath(buildScene.path);
                }
                else
                {
                    scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                }

                scannedCount++;
                bool sceneChanged = false;

                foreach (GameObject rootGo in scene.GetRootGameObjects())
                {
                    Component adManagerComponent = rootGo.GetComponentInChildren(adManagerType, true);
                    if (adManagerComponent == null)
                    {
                        continue;
                    }

                    SerializedObject serializedAdManager = new SerializedObject(adManagerComponent);

                    SetSerializedString(serializedAdManager, "bannerAdUnitId", bannerId);
                    SetSerializedString(serializedAdManager, "interstitialAdUnitId", interstitialId);
                    SetSerializedString(serializedAdManager, "rewardedAdUnitId", rewardedId);
                    SetSerializedString(serializedAdManager, "rewardedInterstitialAdUnitId", rewardedInterstitialId);

                    if (serializedAdManager.ApplyModifiedProperties())
                    {
                        EditorUtility.SetDirty(adManagerComponent);
                        sceneChanged = true;
                        updatedCount++;
                        Debug.Log($"[EasyAdMob] Applied Ad Unit IDs to [AdManager] in scene: {buildScene.path}");
                    }
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                // Only close scenes we opened ourselves for this pass - never touch a scene
                // the user already had open, even if it turned out to have no AdManager.
                if (!wasAlreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            // Restore focus to whatever scene was active before we started.
            if (!string.IsNullOrEmpty(originalSetupPath))
            {
                Scene originalScene = EditorSceneManager.GetSceneByPath(originalSetupPath);
                if (originalScene.IsValid())
                {
                    EditorSceneManager.SetActiveScene(originalScene);
                }
            }

            Debug.Log($"[EasyAdMob] Scanned {scannedCount} scene(s) from Build Settings, updated [AdManager] in {updatedCount}.");
        }

        private bool AnyOpenSceneIsDirty()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    return true;
                }
            }
            return false;
        }

        private void SetSerializedString(SerializedObject target, string propertyName, string value)
        {
            SerializedProperty prop = target.FindProperty(propertyName);
            if (prop != null && !string.IsNullOrEmpty(value))
            {
                prop.stringValue = value.Trim();
            }
        }

        private void SetSerializedBool(SerializedObject target, string propertyName, bool value)
        {
            SerializedProperty prop = target.FindProperty(propertyName);
            if (prop != null && prop.propertyType == SerializedPropertyType.Boolean)
            {
                prop.boolValue = value;
            }
        }

        // Confirmed against the actual GoogleMobileAdsSettings source (internal class,
        // GoogleMobileAds.Editor namespace):
        //   public enum GmaAndroidSdk { Standard = 0, NextGen = 1 }
        //   public bool OverrideDefaultGmaAndroidSdk { get; set; }
        //   public int SelectedGmaAndroidSdk { get; set; }
        // The class is internal, so we can't reference GoogleMobileAdsSettings or GmaAndroidSdk
        // by type at compile time from this assembly - but the property names and the int value
        // for NextGen are now known for certain rather than guessed, so plain reflection on the
        // public properties is safe and doesn't need SerializedObject/enum-name matching at all.
        private const int GMA_ANDROID_SDK_NEXT_GEN = 1;

        /// <summary>
        /// Switches the project to the GMA Next-Gen Android SDK architecture by setting
        /// OverrideDefaultGmaAndroidSdk = true and SelectedGmaAndroidSdk = 1 (NextGen) via
        /// reflection on the confirmed public properties. Returns true on success so the caller
        /// can report it distinctly from "already on Next-Gen" or "failed".
        /// </summary>
        private bool TrySwitchToNextGenAndroidSdk(UnityEngine.Object settingsInstance, Type googleSettingsType)
        {
            PropertyInfo overrideProp = googleSettingsType.GetProperty("OverrideDefaultGmaAndroidSdk", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo selectedProp = googleSettingsType.GetProperty("SelectedGmaAndroidSdk", BindingFlags.Public | BindingFlags.Instance);

            if (overrideProp == null || selectedProp == null || !overrideProp.CanWrite || !selectedProp.CanWrite)
            {
                Debug.LogWarning("[EasyAdMob] Could not find the expected Next-Gen SDK properties on GoogleMobileAdsSettings. Your installed plugin version may differ - please switch it manually in Assets > Google Mobile Ads > Settings.");
                return false;
            }

            overrideProp.SetValue(settingsInstance, true);
            selectedProp.SetValue(settingsInstance, GMA_ANDROID_SDK_NEXT_GEN);

            EditorUtility.SetDirty(settingsInstance);
            AssetDatabase.SaveAssets();
            Debug.Log("[EasyAdMob] Switched Google Mobile Ads Android architecture to Next-Gen SDK.");
            return true;
        }

        /// <summary>
        /// Reads the current Android SDK architecture (Standard vs Next-Gen) for display in the
        /// wizard, without needing to reference the internal GoogleMobileAdsSettings type.
        /// Returns null if the settings asset or properties can't be found/read.
        /// </summary>
        private bool? IsNextGenAndroidSdkActive()
        {
            Type googleSettingsType = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor")
                                   ?? Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Core.Editor");
            if (googleSettingsType == null)
            {
                return null;
            }

            UnityEngine.Object settingsInstance = Resources.Load("GoogleMobileAdsSettings");
            if (settingsInstance == null)
            {
                return null;
            }

            PropertyInfo overrideProp = googleSettingsType.GetProperty("OverrideDefaultGmaAndroidSdk", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo selectedProp = googleSettingsType.GetProperty("SelectedGmaAndroidSdk", BindingFlags.Public | BindingFlags.Instance);
            if (overrideProp == null || selectedProp == null)
            {
                return null;
            }

            bool overrideEnabled = (bool)overrideProp.GetValue(settingsInstance);
            if (!overrideEnabled)
            {
                return false; // Not overridden -> effectively Standard, matching EffectiveGmaAndroidSdk's own fallback.
            }

            int selected = (int)selectedProp.GetValue(settingsInstance);
            return selected == GMA_ANDROID_SDK_NEXT_GEN;
        }

        private GameObject CreateAdManagerInScene()
        {
            Type adManagerType = Type.GetType("EasyAdMob.AdManager, EasyAdMob.Runtime");
            
            UnityEngine.Object existingInstance = null;
            if (adManagerType != null)
            {
                existingInstance = FindObjectOfTypeCustom(adManagerType);
            }

            if (existingInstance != null)
            {
                Selection.activeGameObject = ((Component)existingInstance).gameObject;
                return ((Component)existingInstance).gameObject;
            }

            GameObject go = new GameObject("[AdManager]");
            
            if (adManagerType != null)
            {
                go.AddComponent(adManagerType);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create [AdManager]");
            Selection.activeGameObject = go;
            Debug.Log("[EasyAdMob] Created [AdManager] GameObject in current scene.");
            return go;
        }

        private void GenerateShowcaseScene()
        {
            if (!Directory.Exists("Assets/EasyAdMob/Scenes"))
            {
                Directory.CreateDirectory("Assets/EasyAdMob/Scenes");
                AssetDatabase.Refresh();
            }

            string scenePath = "Assets/EasyAdMob/Scenes/EasyAdMobShowcase.unity";
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject adManagerGo = CreateAdManagerInScene();
            TryApplyIDsToProjectAndScene();

            Type showcaseType = Type.GetType("EasyAdMob.EasyAdMobShowcaseUI, EasyAdMob.Runtime") ?? typeof(EasyAdMobShowcaseUI);
            Component showcaseHelper = adManagerGo.GetComponent(showcaseType);
            if (showcaseHelper == null)
            {
                showcaseHelper = adManagerGo.AddComponent(showcaseType);
            }

            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindObjectOfTypeCustom(typeof(EventSystem)) == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();

                Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemModuleType != null)
                {
                    eventSystem.AddComponent(inputSystemModuleType);
                }
                else
                {
                    eventSystem.AddComponent<StandaloneInputModule>();
                }
            }

            GameObject panel = new GameObject("DemoPanel");
            panel.transform.SetParent(canvasGo.transform, false);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(40, 40, 40, 40);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.1f);
            panelRect.anchorMax = new Vector2(0.8f, 0.9f);

            CreateDemoButton(panel.transform, "Show Banner Ad", showcaseHelper, nameof(EasyAdMobShowcaseUI.ShowBanner));
            CreateDemoButton(panel.transform, "Hide Banner Ad", showcaseHelper, nameof(EasyAdMobShowcaseUI.HideBanner));
            CreateDemoButton(panel.transform, "Show Interstitial Ad", showcaseHelper, nameof(EasyAdMobShowcaseUI.ShowInterstitial));
            CreateDemoButton(panel.transform, "Show Rewarded Ad", showcaseHelper, nameof(EasyAdMobShowcaseUI.ShowRewarded));
            CreateDemoButton(panel.transform, "Show Rewarded Interstitial Ad", showcaseHelper, nameof(EasyAdMobShowcaseUI.ShowRewardedInterstitial));

            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[EasyAdMob] Successfully created Showcase Scene at {scenePath}");
        }

        private void CreateDemoButton(Transform parent, string labelText, Component targetComponent, string methodName)
        {
            GameObject buttonGo = new GameObject(labelText);
            buttonGo.transform.SetParent(parent, false);

            Image img = buttonGo.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.9f, 1f);

            Button btn = buttonGo.AddComponent<Button>();

            LayoutElement layoutElement = buttonGo.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 50;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(buttonGo.transform, false);
            Text txt = textGo.AddComponent<Text>();
            txt.text = labelText;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 18;

            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            MethodInfo method = targetComponent.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0);

            if (method != null)
            {
                UnityAction action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), targetComponent, method);
                UnityEventTools.AddPersistentListener(btn.onClick, action);
            }
            else
            {
                Debug.LogWarning($"[EasyAdMob] Could not find parameterless method '{methodName}' on {targetComponent.GetType().Name}.");
            }
        }

        private static UnityEngine.Object FindObjectOfTypeCustom(Type type)
        {
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType(type);
#else
            return FindObjectOfType(type);
#endif
        }

        private bool HasScriptingDefineSymbol()
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(buildTarget, out string[] defines);
            return defines.Contains(SCRIPTING_DEFINE_SYMBOL);
        }

        private static void AddScriptingDefineSymbol()
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(buildTarget, out string[] defines);

            if (!defines.Contains(SCRIPTING_DEFINE_SYMBOL))
            {
                string[] newDefines = defines.Append(SCRIPTING_DEFINE_SYMBOL).ToArray();
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, newDefines);
                Debug.Log($"[EasyAdMob] Added scripting define symbol: {SCRIPTING_DEFINE_SYMBOL}");
            }
        }
    }
}
#endif