#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Networking;

namespace EasyAdMob.Editor
{
    public class AdManagerSetupMenu : EditorWindow
    {
        private const string ADMOB_PACKAGE_URL = "https://github.com/googleads/googleads-mobile-unity/releases/download/v11.4.0/GoogleMobileAds-v11.4.0.unitypackage";
        private const string TEMP_FILE_PATH = "Temp/GoogleMobileAds.unitypackage";
        private const string SCRIPTING_DEFINE_SYMBOL = "EASY_ADMOB_GOOGLE_MOBILE_ADS";

        private bool isDownloading = false;
        private float downloadProgress = 0f;

        // --- ID FIELDS ---
        private string androidAppId = "";
        private string iosAppId = "";
        private string bannerId = "ca-app-pub-3940256099942544/6300978111";
        private string interstitialId = "ca-app-pub-3940256099942544/1033173712";
        private string rewardedId = "ca-app-pub-3940256099942544/5224354917";
        private string rewardedInterstitialId = "ca-app-pub-3940256099942544/5354046379";

        [MenuItem("Tools/EasyAdMob/Setup Wizard", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdManagerSetupMenu>("EasyAdMob Setup");
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("EasyAdMob Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use this wizard to install dependencies, configure App/Ad IDs, and setup AdManager in your active scene.", MessageType.Info);
            
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
                if (GUILayout.Button(isDownloading ? $"Downloading ({downloadProgress * 100:F0}%)..." : "Download & Install Google Mobile Ads", GUILayout.Height(28)))
                {
                    DownloadAndInstallAdMob();
                }
                GUI.enabled = true;
            }

            bool hasDefine = HasScriptingDefineSymbol();
            if (!hasDefine && GUILayout.Button("Add Scripting Define Symbol"))
            {
                AddScriptingDefineSymbol();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // --- STEP 2: ID CONFIGURATION ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step 2: App & Ad Unit IDs", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("Google Mobile Ads App IDs", EditorStyles.miniBoldLabel);
            androidAppId = EditorGUILayout.TextField("Android App ID", androidAppId);
            iosAppId = EditorGUILayout.TextField("iOS App ID", iosAppId);

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Ad Unit IDs (Defaults are Test IDs)", EditorStyles.miniBoldLabel);
            bannerId = EditorGUILayout.TextField("Banner ID", bannerId);
            interstitialId = EditorGUILayout.TextField("Interstitial ID", interstitialId);
            rewardedId = EditorGUILayout.TextField("Rewarded ID", rewardedId);
            rewardedInterstitialId = EditorGUILayout.TextField("Rewarded Interstitial ID", rewardedInterstitialId);

            GUILayout.Space(5);
            if (GUILayout.Button("Apply IDs to AdManager & Settings", GUILayout.Height(28)))
            {
                ApplyIDsToProjectAndScene();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // --- STEP 3: SCENE SETUP ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Step 3: Scene Setup", EditorStyles.boldLabel);

            if (GUILayout.Button("Create [AdManager] in Active Scene", GUILayout.Height(28)))
            {
                CreateAdManagerInScene();
                ApplyIDsToProjectAndScene();
            }
            EditorGUILayout.EndVertical();
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
            // 1. Configure Google Mobile Ads Settings Asset
            Type googleSettingsType = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor")
                                   ?? Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Core.Editor");
        
            if (googleSettingsType != null)
            {
                // Force-load the asset instance directly from Resources/AssetDatabase if possible
                UnityEngine.Object settingsInstance = Resources.Load("GoogleMobileAdsSettings");
        
                // If not loaded, fallback to property getter
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
                    Debug.Log("[EasyAdMob] Successfully updated Google Mobile Ads App IDs!");
                }
                else
                {
                    Debug.LogWarning("[EasyAdMob] Could not find or instantiate GoogleMobileAdsSettings asset. Open 'Assets > Google Mobile Ads > Settings' once in Unity to create it.");
                }
            }
            else
            {
                Debug.LogWarning("[EasyAdMob] GoogleMobileAdsSettings type not found. Ensure Google Mobile Ads SDK is installed.");
            }
        
            // 2. Configure AdManager in Scene
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
                prop.stringValue = value;
            }
        }

        private void CreateAdManagerInScene()
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
                Debug.LogWarning("[EasyAdMob] An AdManager object already exists in this scene. Selected existing instance.");
                return;
            }

            GameObject go = new GameObject("[AdManager]");
            
            if (adManagerType != null)
            {
                go.AddComponent(adManagerType);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create [AdManager]");
            Selection.activeGameObject = go;
            Debug.Log("[EasyAdMob] Created [AdManager] GameObject in current scene.");
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