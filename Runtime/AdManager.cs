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

        public static event Action OnBannerAdLoaded;
        public static event Action OnInterstitialAdLoaded;
        public static event Action OnRewardedAdLoaded;
        public static event Action OnRewardedInterstitialAdLoaded;

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
        private bool isBannerAdLoaded;
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
            isBannerAdLoaded = false;

            int deviceWidth = MobileAds.Utils.GetDeviceSafeWidth();

            AdSize adSize =
                AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(deviceWidth);

            bannerView = new BannerView(
                bannerAdUnitId,
                adSize,
                bannerPosition
            );

            bannerView.OnBannerAdLoaded += () =>
            {
                isBannerAdLoaded = true;
                Debug.Log("[AdManager] Banner loaded successfully.");
                OnBannerAdLoaded?.Invoke();
            };

            bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
            {
                isBannerAdLoaded = false;
                Debug.LogWarning(
                    $"[AdManager] Banner ad failed to load: {error?.GetMessage() ?? "unknown error"}"
                );
            };

            AdRequest request = new AdRequest();
            bannerView.LoadAd(request);
#endif
        }

        public bool IsBannerAdReady()
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            return bannerView != null && isBannerAdLoaded;
#else
            return false;
#endif
        }

        // Banner loading is asynchronous (BannerView.LoadAd fires OnBannerAdLoaded/
        // OnBannerAdLoadFailed some time later), so unlike the full-screen ad types this
        // can't bundle a synchronous readiness check the same way - there's nothing to
        // instantly show yet. If no banner is loaded, this starts a load and lets the
        // caller know via onAdUnavailable so they aren't left silently waiting; the banner
        // will still appear once loaded if the caller calls ShowBannerAd() again, or you can
        // subscribe to IsBannerAdReady() polling / your own loaded-state UI as needed.
        public void ShowBannerAd(Action onAdUnavailable = null)
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (IsBannerAdReady())
            {
                bannerView.Show();
            }
            else
            {
                Debug.LogWarning("[AdManager] ShowBannerAd called but no banner ad was loaded yet. Loading one now.");
                onAdUnavailable?.Invoke();
                LoadBannerAd();
            }
#else
            onAdUnavailable?.Invoke();
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
                isBannerAdLoaded = false;
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
                    Debug.LogWarning($"[AdManager] Rewarded ad failed to load: {error?.GetMessage() ?? "unknown error"}");
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
        //
        // Readiness is checked internally now, so callers don't need to call
        // IsRewardedAdReady() themselves first - IsRewardedAdReady() is still public
        // for anyone who wants to gate UI on it (e.g. disabling a "Watch Ad" button
        // while nothing is loaded). If no ad is available, onAdUnavailable fires
        // instead of onRewardSuccess (silently doing nothing here would leave the
        // caller with no way to know the reward never happened), and a fresh ad
        // load is kicked off in the background either way.
        public void ShowRewardedAd(Action onRewardSuccess, Action onAdUnavailable = null)
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (IsRewardedAdReady())
            {
                bool rewardGranted = false;
                RewardedAd adToShow = rewardedAd;

                adToShow.OnAdFullScreenContentClosed += () =>
                {
                    if (rewardGranted)
                    {
                        onRewardSuccess?.Invoke();
                    }
                };

                // If the ad fails to actually open (rare - e.g. a network blip between load and
                // show), OnAdFullScreenContentClosed never fires for this attempt, so without
                // this handler onRewardSuccess/onAdUnavailable would simply never be called and
                // the caller would be left hanging with no callback and no error. Per Google's
                // own samples, the correct response is to surface it and load a fresh ad.
                adToShow.OnAdFullScreenContentFailed += (AdError error) =>
                {
                    Debug.LogWarning($"[AdManager] Rewarded ad failed to show: {error?.GetMessage() ?? "unknown error"}");
                    onAdUnavailable?.Invoke();
                    LoadRewardedAd();
                };

                adToShow.Show((Reward reward) =>
                {
                    Debug.Log($"[AdManager] Reward earned: {reward.Type}");
                    rewardGranted = true; // record it, act on close instead
                });
                rewardedAd = null;
            }
            else
            {
                Debug.LogWarning("[AdManager] ShowRewardedAd called but no rewarded ad was ready. Loading one for next time.");
                onAdUnavailable?.Invoke();
                LoadRewardedAd();
            }
#else
            onAdUnavailable?.Invoke();
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
                    Debug.LogWarning($"[AdManager] Rewarded interstitial ad failed to load: {error?.GetMessage() ?? "unknown error"}");
                    return;
                }

                rewardedInterstitialAd = ad;
                OnRewardedInterstitialAdLoaded?.Invoke();
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
        // instead of the Show() reward callback. Also same readiness-bundling as
        // ShowRewardedAd - see its comment for why onAdUnavailable exists.
        public void ShowRewardedInterstitialAd(Action onRewardSuccess, Action onAdUnavailable = null)
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (IsRewardedInterstitialAdReady())
            {
                bool rewardGranted = false;
                RewardedInterstitialAd adToShow = rewardedInterstitialAd;

                adToShow.OnAdFullScreenContentClosed += () =>
                {
                    if (rewardGranted)
                    {
                        onRewardSuccess?.Invoke();
                    }
                };

                // See ShowRewardedAd for why this handler exists.
                adToShow.OnAdFullScreenContentFailed += (AdError error) =>
                {
                    Debug.LogWarning($"[AdManager] Rewarded interstitial ad failed to show: {error?.GetMessage() ?? "unknown error"}");
                    onAdUnavailable?.Invoke();
                    LoadRewardedInterstitialAd();
                };

                adToShow.Show((Reward reward) =>
                {
                    rewardGranted = true;
                });
                rewardedInterstitialAd = null;
            }
            else
            {
                Debug.LogWarning("[AdManager] ShowRewardedInterstitialAd called but no ad was ready. Loading one for next time.");
                onAdUnavailable?.Invoke();
                LoadRewardedInterstitialAd();
            }
#else
            onAdUnavailable?.Invoke();
#endif
        }

        public void ShowRewardedInterstitialAd() => ShowRewardedInterstitialAd(null, null);

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
                    Debug.LogWarning($"[AdManager] Interstitial ad failed to load: {error?.GetMessage() ?? "unknown error"}");
                    return;
                }

                interstitialAd = ad;
                OnInterstitialAdLoaded?.Invoke();
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

        public void ShowInterstitialAd(Action onAdUnavailable = null)
        {
#if EASY_ADMOB_GOOGLE_MOBILE_ADS
            if (IsInterstitialAdReady())
            {
                InterstitialAd adToShow = interstitialAd;

                // See ShowRewardedAd for why this handler exists - if Show() fails to actually
                // open, this is the only signal the caller gets that nothing happened.
                adToShow.OnAdFullScreenContentFailed += (AdError error) =>
                {
                    Debug.LogWarning($"[AdManager] Interstitial ad failed to show: {error?.GetMessage() ?? "unknown error"}");
                    onAdUnavailable?.Invoke();
                    LoadInterstitialAd();
                };

                adToShow.Show();
                interstitialAd = null;
            }
            else
            {
                Debug.LogWarning("[AdManager] ShowInterstitialAd called but no ad was ready. Loading one for next time.");
                onAdUnavailable?.Invoke();
                LoadInterstitialAd();
            }
#else
            onAdUnavailable?.Invoke();
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