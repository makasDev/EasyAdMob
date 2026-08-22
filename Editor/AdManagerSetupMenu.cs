#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
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
                // Execute outside OnGUI pass to prevent GUILayout frame mismatch errors
                EditorApplication.delayCall += () => GenerateShowcaseScene();
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

            // 1. Create AdManager instance & attach runtime showcase helper
            GameObject adManagerGo = CreateAdManagerInScene();
            ApplyIDsToProjectAndScene();

            EasyAdMobShowcaseUI showcaseHelper = adManagerGo.GetComponent<EasyAdMobShowcaseUI>();
            if (showcaseHelper == null)
            {
                showcaseHelper = adManagerGo.AddComponent<EasyAdMobShowcaseUI>();
            }

            // 2. Setup Canvas
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // 3. Setup EventSystem with New Input System check
            if (FindObjectOfType<EventSystem>() == null)
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

            // 4. UI Panel Layout
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

            // 5. Create Buttons targeting EasyAdMobShowcaseUI methods
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

// Runtime component attached automatically to [AdManager] during scene generation
public class EasyAdMobShowcaseUI : MonoBehaviour
{
    public void ShowBanner() => InvokeAdManagerMethod("ShowBannerAd");
    public void HideBanner() => InvokeAdManagerMethod("HideBannerAd");
    public void ShowInterstitial() => InvokeAdManagerMethod("ShowInterstitialAd");

    public void ShowRewarded()
    {
        InvokeAdManagerRewardedMethod("ShowRewardedAd");
    }

    public void ShowRewardedInterstitial()
    {
        InvokeAdManagerRewardedMethod("ShowRewardedInterstitialAd");
    }

    private void InvokeAdManagerMethod(string methodName)
    {
        Type adManagerType = Type.GetType("EasyAdMob.AdManager, EasyAdMob.Runtime");
        if (adManagerType != null)
        {
            var instance = FindObjectOfType(adManagerType);
            if (instance != null)
            {
                MethodInfo method = adManagerType.GetMethods().FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0);
                method?.Invoke(instance, null);
            }
        }
    }

    private void InvokeAdManagerRewardedMethod(string methodName)
    {
        Type adManagerType = Type.GetType("EasyAdMob.AdManager, EasyAdMob.Runtime");
        if (adManagerType != null)
        {
            var instance = FindObjectOfType(adManagerType);
            if (instance != null)
            {
                MethodInfo method = adManagerType.GetMethods().FirstOrDefault(m => m.Name == methodName);
                if (method != null)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        method.Invoke(instance, null);
                    }
                    else if (parameters.Length == 1)
                    {
                        Type paramType = parameters[0].ParameterType;
                        object callback = null;

                        if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Action<>))
                        {
                            Type rewardType = paramType.GetGenericArguments()[0];
                            MethodInfo dummyMethod = typeof(EasyAdMobShowcaseUI).GetMethod(nameof(OnRewardReceived), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(rewardType);
                            callback = Delegate.CreateDelegate(paramType, this, dummyMethod);
                        }

                        method.Invoke(instance, new object[] { callback });
                    }
                }
            }
        }
    }

    private void OnRewardReceived<T>(T reward)
    {
        Debug.Log($"[EasyAdMob Showcase] Reward earned: {reward}");
    }
}
#endif