using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace EasyAdMob
{
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
                var instance = FindAdManagerInstance(adManagerType);
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
                var instance = FindAdManagerInstance(adManagerType);
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

        private UnityEngine.Object FindAdManagerInstance(Type type)
        {
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType(type);
#else
            return FindObjectOfType(type);
#endif
        }

        private void OnRewardReceived<T>(T reward)
        {
            Debug.Log($"[EasyAdMob Showcase] Reward earned: {reward}");
        }
    }
}