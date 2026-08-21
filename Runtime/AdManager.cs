using UnityEngine;
using GoogleMobileAds.Api;
using System;
using System.Collections;

namespace MobileAds.Package
{
    public class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        [Header("Banner Settings")]
        [SerializeField] private string bannerAdUnitId = "ca-app-pub-3940256099942544/6300978111"; // Default Test ID
        [SerializeField] private AdPosition bannerPosition = AdPosition.Bottom;
        [SerializeField] private bool loadBannerOnStart = false;

        [Header("Interstitial Settings")]
        [SerializeField] private string interstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Default Test ID

        [Header("Rewarded Settings")]
        [SerializeField] private string rewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917"; // Default Test ID

        [Header("Rewarded Interstitial Settings")]
        [SerializeField] private string rewardedInterstitialAdUnitId = "ca-app-pub-3940256099942544/5354046379"; // Default Test ID

        private BannerView bannerView;
        private InterstitialAd interstitialAd;
        private RewardedAd rewardedAd;
        private RewardedInterstitialAd rewardedInterstitialAd;

        public static event Action OnRewardedAdLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            bool initialized = false;
            MobileAds.Initialize((InitializationStatus initStatus) => { initialized = true; });

            yield return new WaitUntil(() => initialized);

            if (loadBannerOnStart)
            {
                LoadBannerAd();
            }

            LoadRewardedAd();
            LoadInterstitialAd();
            LoadRewardedInterstitialAd();
        }

        // ===================== BANNER ADS =====================

        public void LoadBannerAd()
        {
            // Clean up existing banner before creating a new one
            DestroyBannerAd();

            AdSize adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            bannerView = new BannerView(bannerAdUnitId, adSize, bannerPosition);

            AdRequest request = new AdRequest();
            bannerView.LoadAd(request);
        }

        public void ShowBannerAd()
        {
            if (bannerView != null)
            {
                bannerView.Show();
            }
            else
            {
                LoadBannerAd();
            }
        }

        public void HideBannerAd()
        {
            bannerView?.Hide();
        }

        public void DestroyBannerAd()
        {
            if (bannerView != null)
            {
                bannerView.Destroy();
                bannerView = null;
            }
        }

        // ===================== REWARDED ADS (Standard) =====================

        public void LoadRewardedAd()
        {
            AdRequest request = new AdRequest();
            RewardedAd.Load(rewardedAdUnitId, request, (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    rewardedAd = null;
                    return;
                }

                rewardedAd = ad;
                OnRewardedAdLoaded?.Invoke();
                rewardedAd.OnAdFullScreenContentClosed += () => LoadRewardedAd();
            });
        }

        public bool IsRewardedAdReady() => rewardedAd != null && rewardedAd.CanShowAd();

        public void ShowRewardedAd(Action onRewardSuccess)
        {
            if (IsRewardedAdReady())
            {
                rewardedAd.Show((Reward reward) =>
                {
                    Debug.Log($"[AdManager] Reward earned: {reward.Type}");
                    onRewardSuccess?.Invoke();
                });
                rewardedAd = null;
            }
            else
            {
                LoadRewardedAd();
            }
        }

        // ===================== REWARDED INTERSTITIAL ADS =====================

        public void LoadRewardedInterstitialAd()
        {
            AdRequest request = new AdRequest();
            RewardedInterstitialAd.Load(rewardedInterstitialAdUnitId, request, (RewardedInterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    rewardedInterstitialAd = null;
                    return;
                }

                rewardedInterstitialAd = ad;
                rewardedInterstitialAd.OnAdFullScreenContentClosed += () => LoadRewardedInterstitialAd();
            });
        }

        public bool IsRewardedInterstitialAdReady() => rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd();

        public void ShowRewardedInterstitialAd(Action onRewardSuccess)
        {
            if (IsRewardedInterstitialAdReady())
            {
                rewardedInterstitialAd.Show((Reward reward) =>
                {
                    onRewardSuccess?.Invoke();
                });
                rewardedInterstitialAd = null;
            }
            else
            {
                LoadRewardedInterstitialAd();
            }
        }

        public void ShowRewardedInterstitialAd() => ShowRewardedInterstitialAd(null);

        // ===================== INTERSTITIAL ADS =====================

        public void LoadInterstitialAd()
        {
            AdRequest request = new AdRequest();
            InterstitialAd.Load(interstitialAdUnitId, request, (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    interstitialAd = null;
                    return;
                }

                interstitialAd = ad;
                interstitialAd.OnAdFullScreenContentClosed += () => LoadInterstitialAd();
            });
        }

        public bool IsInterstitialAdReady() => interstitialAd != null && interstitialAd.CanShowAd();

        public void ShowInterstitialAd()
        {
            if (IsInterstitialAdReady())
            {
                interstitialAd.Show();
                interstitialAd = null;
            }
            else
            {
                LoadInterstitialAd();
            }
        }

        private void OnDestroy()
        {
            DestroyBannerAd();
            rewardedAd?.Destroy();
            interstitialAd?.Destroy();
            rewardedInterstitialAd?.Destroy();
        }
    }
}