#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace EasyAdMob.Editor
{
    public class AdManagerSetupMenu : EditorWindow
    {
        private const string ADMOB_PACKAGE_URL = "https://github.com/googleads/googleads-mobile-unity/releases/download/v11.4.0/GoogleMobileAds-v11.4.0.unitypackage";
        private const string TEMP_FILE_PATH = "Temp/GoogleMobileAds.unitypackage";
        private const string SCRIPTING_DEFINE_SYMBOL = "EASY_ADMOB_GOOGLE_MOBILE_ADS";

        // --- OFFICIAL TEST IDS ---
        private const string TEST_ANDROID_APP_ID = "ca-app-pub-3940256099942544~3347511713";
        private const string TEST_IOS_APP_ID = "ca-app-pub-3940256099942544~1458002511";
        private const string TEST_BANNER_ID = "ca-app-pub-3940256099942544/6300978111";
        private const string TEST_INTERSTITIAL_ID = "ca-app-pub-3940256099942544/1033173712";
        private const string TEST_REWARDED_ID = "ca-app-pub-3940256099942544/5224354917";
        private const string TEST_REWARDED_INTERSTITIAL_ID = "ca-app-pub-3940256099942544/5354046379";

        private bool isDownloading = false;
        private float downloadProgress = 0f;

        // --- ID FIELDS ---
        private string androidAppId = TEST_ANDROID_APP_ID;
        private string iosAppId = TEST_IOS_APP_ID;
        private string bannerId = TEST_BANNER_ID;
        private string interstitialId = TEST_INTERSTITIAL_ID;
        private string rewardedId = TEST_REWARDED_ID;
        private string rewardedInterstitialId = TEST_REWARDED_INTERSTITIAL_ID;

        [MenuItem("Tools/EasyAdMob/Setup Wizard", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdManagerSetupMenu>("EasyAdMob Setup");
            window.minSize = new Vector2(440, 620);
            window.Show();
        }

        private void OnGUI()
        {
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150f;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("EasyAdMob Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use this wizard to install dependencies, configure App/Ad IDs, and setup AdManager in your scene.", MessageType.Info);
            
            GUILayout.Space(10);

            // --- STEP 1: DEPENDENCIES ---
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

            // --- STEP 2: ID CONFIGURATION ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step 2: App & Ad Unit IDs", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.LabelField("Google Mobile Ads App IDs", EditorStyles.miniBoldLabel);
            androidAppId = EditorGUILayout.TextField("Android App ID", androidAppId);
            iosAppId = EditorGUILayout.TextField("iOS App ID", iosAppId);

            GUILayout.Space(6);
            EditorGUILayout.LabelField("Ad Unit IDs", EditorStyles.miniBoldLabel);
            bannerId = EditorGUILayout.TextField("Banner ID", bannerId);
            interstitialId = EditorGUILayout.TextField("Interstitial ID", interstitialId);
            rewardedId = EditorGUILayout.TextField("Rewarded ID", rewardedId);
            rewardedInterstitialId = EditorGUILayout.TextField("Rewarded Interstitial ID", rewardedInterstitialId);

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill Test IDs", GUILayout.Height(30)))
            {
                FillTestIDs();
            }

            if (GUILayout.Button("Apply IDs to Project & Scene", GUILayout.Height(30)))
            {
                ApplyIDsToProjectAndScene();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // --- STEP 3: SCENE SETUP ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step 3: Scene Setup", EditorStyles.boldLabel);
            GUILayout.Space(4);

            if (GUILayout.Button("Create [AdManager] in Active Scene", GUILayout.Height(30)))
            {
                CreateAdManagerInScene();
                ApplyIDsToProjectAndScene();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Generate Showcase Demo Scene", GUILayout.Height(30)))
            {
                GenerateShowcaseScene();
            }
            EditorGUILayout.EndVertical();

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        private void FillTestIDs()
        {
            androidAppId = TEST_ANDROID_APP_ID;
            iosAppId = TEST_IOS_APP_ID;
            bannerId = TEST_BANNER_ID;
            interstitialId = TEST_INTERSTITIAL_ID;
            rewardedId = TEST_REWARDED_ID;
            rewardedInterstitialId = TEST_REWARDED_INTERSTITIAL_ID;
            Debug.Log("[EasyAdMob] Filled fields with default AdMob Test IDs.");
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
                UnityEngine.Object adManagerObj = FindObjectOfType(adManagerType);
                if (adManagerObj != null)
                {
                    SerializedObject serializedAdManager = new SerializedObject(adManagerObj);

                    SetSerializedString(serializedAdManager, "bannerAdUnitId", bannerId);
                    SetSerializedString(serializedAdManager, "interstitialAdUnitId", interstitialId);
                    SetSerializedString(serializedAdManager, "rewardedAdUnitId", rewardedId);
                    SetSerializedString(serializedAdManager, "rewardedInterstitialAdUnitId", rewardedInterstitialId);

                    serializedAdManager.ApplyModifiedProperties();
                    EditorUtility.SetDirty(adManagerObj);
                    Debug.Log("[EasyAdMob] Applied Ad Unit IDs to [AdManager] in active scene.");
                }
            }
        }

        private void SetSerializedString(SerializedObject target, string propertyName, string value)
        {
            SerializedProperty prop = target.FindProperty(propertyName);
            if (prop != null && !string.IsNullOrEmpty(value))
            {
                prop.stringValue = value.Trim();
            }
        }

        private GameObject CreateAdManagerInScene()
        {
            Type adManagerType = Type.GetType("EasyAdMob.AdManager, EasyAdMob.Runtime");
            
            UnityEngine.Object existingInstance = null;
            if (adManagerType != null)
            {
                existingInstance = FindObjectOfType(adManagerType);
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

            // 1. Create AdManager instance
            GameObject adManagerGo = CreateAdManagerInScene();
            ApplyIDsToProjectAndScene();

            // 2. Setup Canvas
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            // 3. UI Panel Layout
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

            // 4. Create Buttons
            Type adManagerType = Type.GetType("EasyAdMob.AdManager, EasyAdMob.Runtime");
            Component adManagerComp = adManagerGo.GetComponent(adManagerType);

            CreateDemoButton(panel.transform, "Show Banner Ad", adManagerComp, "ShowBannerAd");
            CreateDemoButton(panel.transform, "Hide Banner Ad", adManagerComp, "HideBannerAd");
            CreateDemoButton(panel.transform, "Show Interstitial Ad", adManagerComp, "ShowInterstitialAd");
            CreateDemoButton(panel.transform, "Show Rewarded Ad", adManagerComp, "ShowRewardedAd");
            CreateDemoButton(panel.transform, "Show Rewarded Interstitial Ad", adManagerComp, "ShowRewardedInterstitialAd");

            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[EasyAdMob] Created Showcase Scene at {scenePath}");
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

            if (targetComponent != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), targetComponent, methodName) as UnityEngine.Events.UnityAction);
            }
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