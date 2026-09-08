using UnityEngine;

namespace EasyAdMob
{
    /// <summary>
    /// Thin wrapper the Showcase Demo Scene's buttons call into. Talks to AdManager directly
    /// (same assembly, so no reflection needed) rather than looking methods up by name and
    /// parameter count - that approach broke silently as soon as AdManager's Show...Ad methods
    /// gained optional parameters and overloads, since a reflection filter written for the old
    /// zero-argument signatures no longer matched anything and calls quietly became no-ops.
    /// </summary>
    public class EasyAdMobShowcaseUI : MonoBehaviour
    {
        public void ShowBanner()
        {
            AdManager.Instance?.ShowBannerAd(
                onAdUnavailable: () => Debug.Log("[EasyAdMob Showcase] Banner ad not loaded yet - loading now."));
        }

        public void HideBanner()
        {
            AdManager.Instance?.HideBannerAd();
        }

        public void ShowInterstitial()
        {
            AdManager.Instance?.ShowInterstitialAd(
                onAdUnavailable: () => Debug.Log("[EasyAdMob Showcase] Interstitial ad not ready."));
        }

        public void ShowRewarded()
        {
            AdManager.Instance?.ShowRewardedAd(
                onRewardSuccess: () => Debug.Log("[EasyAdMob Showcase] Reward earned!"),
                onAdUnavailable: () => Debug.Log("[EasyAdMob Showcase] Rewarded ad not ready."));
        }

        public void ShowRewardedInterstitial()
        {
            AdManager.Instance?.ShowRewardedInterstitialAd(
                onRewardSuccess: () => Debug.Log("[EasyAdMob Showcase] Reward earned!"),
                onAdUnavailable: () => Debug.Log("[EasyAdMob Showcase] Rewarded interstitial ad not ready."));
        }
    }
}