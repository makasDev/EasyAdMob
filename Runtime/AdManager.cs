using UnityEngine;
using System;
using System.Collections;

#if EASY_ADMOB_GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
#endif

namespace EasyAdMob
{
    public class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        public static event Action OnRewardedAdLoaded;

        [Header("Banner Settings")]
        [SerializeField] private string bannerAdUnitId = "ca-app-pub-3940256099942544/6300978111"; // Default Test ID
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
        [SerializeField] private AdPosition bannerPosition = AdPosition.Bottom;
#endif
        [SerializeField] private bool loadBannerOnStart = false;

        [Header("Interstitial Settings")]
        [SerializeField] private string interstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Default Test ID

        [Header("Rewarded Settings")]
        [SerializeField] private string rewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917"; // Default Test ID

        [Header("Rewarded Interstitial Settings")]
        [SerializeField] private string rewardedInterstitialAdUnitId = "ca-app-pub-3940256099942544/5354046379"; // Default Test ID

#if EASY_ADMOB_GOOGLE_MOBILE_ADS
        private BannerView bannerView;
        private InterstitialAd interstitialAd;
        private RewardedAd rewardedAd;
        private RewardedInterstitialAd rewardedInterstitialAd;
#endif

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
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
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
#else
            Debug.LogWarning("[EasyAdMob] Google Mobile Ads SDK is missing or EASY_ADMOB_GOOGLE_MOBILE_ADS symbol is not defined.");
            yield break;
#endif
        }

        // ===================== BANNER ADS =====================

        public void LoadBannerAd()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            DestroyBannerAd();

            AdSize adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            bannerView = new BannerView(bannerAdUnitId, adSize, bannerPosition);

            AdRequest request = new AdRequest();
            bannerView.LoadAd(request);
#endif
        }

        public void ShowBannerAd()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (bannerView != null)
            {
                bannerView.Show();
            }
            else
            {
                LoadBannerAd();
            }
#endif
        }

        public void HideBannerAd()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            bannerView?.Hide();
#endif
        }

        public void DestroyBannerAd()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (bannerView != null)
            {
                bannerView.Destroy();
                bannerView = null;
            }
#endif
        }

        // ===================== REWARDED ADS (Standard) =====================

        public void LoadRewardedAd()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
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
#endif
        }

        public bool IsRewardedAdReady()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            return rewardedAd != null && rewardedAd.CanShowAd();
#else
            return false;
#endif
        }

        // NOTE: onRewardSuccess is now invoked from OnAdFullScreenContentClosed,
        // not from the Show() reward callback. On current GMA versions the reward
        // callback can fire several seconds before the user actually dismisses the
        // ad (taps the X), so any gameplay logic gated on it was running while the
        // ad was still on screen. Gating on close instead makes sure game state
        // only changes once the player is actually back looking at the game.
        public void ShowRewardedAd(Action onRewardSuccess)
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (IsRewardedAdReady())
            {
                bool rewardGranted = false;

                rewardedAd.OnAdFullScreenContentClosed += () =>
                {
                    if (rewardGranted)
                    {
                        onRewardSuccess?.Invoke();
                    }
                };

                rewardedAd.Show((Reward reward) =>
                {
                    Debug.Log($"[AdManager] Reward earned: {reward.Type}");
                    rewardGranted = true; // record it, act on close instead
                });
                rewardedAd = null;
            }
            else
            {
                LoadRewardedAd();
            }
#endif
        }

        // ===================== REWARDED INTERSTITIAL ADS =====================

        public void LoadRewardedInterstitialAd()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
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
#endif
        }

        public bool IsRewardedInterstitialAdReady()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            return rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd();
#else
            return false;
#endif
        }

        // Same fix as ShowRewardedAd: gate onRewardSuccess on OnAdFullScreenContentClosed
        // instead of the Show() reward callback.
        public void ShowRewardedInterstitialAd(Action onRewardSuccess)
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (IsRewardedInterstitialAdReady())
            {
                bool rewardGranted = false;

                rewardedInterstitialAd.OnAdFullScreenContentClosed += () =>
                {
                    if (rewardGranted)
                    {
                        onRewardSuccess?.Invoke();
                    }
                };

                rewardedInterstitialAd.Show((Reward reward) =>
                {
                    rewardGranted = true;
                });
                rewardedInterstitialAd = null;
            }
            else
            {
                LoadRewardedInterstitialAd();
            }
#endif
        }

        public void ShowRewardedInterstitialAd() => ShowRewardedInterstitialAd(null);

        // ===================== INTERSTITIAL ADS =====================

        public void LoadInterstitialAd()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
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
#endif
        }

        public bool IsInterstitialAdReady()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            return interstitialAd != null && interstitialAd.CanShowAd();
#else
            return false;
#endif
        }

        public void ShowInterstitialAd()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (IsInterstitialAdReady())
            {
                interstitialAd.Show();
                interstitialAd = null;
            }
            else
            {
                LoadInterstitialAd();
            }
#endif
        }

        private void OnDestroy()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            DestroyBannerAd();
            rewardedAd?.Destroy();
            interstitialAd?.Destroy();
            rewardedInterstitialAd?.Destroy();
#endif
        }
    }
}