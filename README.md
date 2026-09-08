# EasyAdMob for Unity

A lightweight, plug-and-play Unity Package Manager (UPM) wrapper for **Google Mobile Ads (AdMob)**. Designed to eliminate boilerplate code and streamline setup across your Unity games with standard AdMob formats (Adaptive Banners, Interstitials, Rewarded, and Rewarded Interstitials).

---

## Features

* **One-Click Scene Setup:** Custom Unity Editor wizard (`Tools > EasyAdMob > Setup Wizard`) to instantiate and configure `AdManager` instantly.
* **Auto Showcase Scene:** Generates an isolated UI demo scene equipped with pre-wired buttons to test all ad formats right out of the box.
* **Dependency Automation:** Built-in SDK downloader directly inside Unity Editor.
* **Next-Gen SDK Switching:** One-click migration to Google's Next-Gen Android SDK architecture, right from the wizard.
* **Persistent, Validated IDs:** App/Ad Unit IDs you enter are validated against AdMob's real ID format, remembered between sessions, and clearly marked as test vs. custom in the wizard UI.
* **Project-Wide ID Sync:** Applying IDs updates every `AdManager` across every scene in your Build Settings, not just the one you have open.
* **Clean Singleton Architecture:** Call ads anywhere in your codebase with `using EasyAdMob;`.
* **Delegate Rewards:** Lambda/action callbacks to grant in-game items upon ad completion, with readiness checked internally.

---

## Installation

### Unity Package Manager

1. Open your Unity project.
2. Go to **Window > Package Manager**.
3. Click the **`+`** icon and select **Add package from git URL...**
4. Paste the repository URL:
```bash
https://github.com/makasdev/EasyAdMob.git

```


5. Click **Add**.

---

## Editor Setup

Open the Setup Wizard via **Tools > EasyAdMob > Setup Wizard**, then work through the four steps:

1. **Dependencies & Symbols** — Click **Download & Install Google Mobile Ads** to automatically fetch the SDK, then add the required scripting define symbol if prompted.
2. **Android SDK Architecture** — Optionally switch to Google's **Next-Gen Android SDK** with one click. The wizard shows which architecture is currently active; switching requires Android API level 24+.
3. **App & Ad Unit IDs** — Enter your real App/Ad Unit IDs, or click **Fill Test IDs** to populate Google's official test units. Fields you haven't customized show as grayed-out test ID placeholders; anything you type is validated against AdMob's real ID format before you can apply it. Click **Apply IDs to Project & Scene** to write your IDs into the project settings and every `AdManager` across every scene in your Build Settings — this also keeps the Gradle build pre-processor and kotlinx.coroutines packaging options enabled.
4. **Scene Setup** — Click **Create [AdManager] in Active Scene** to drop a configured `AdManager` into your current scene, or **Generate Showcase Demo Scene** to build a standalone scene with pre-wired buttons for every ad format.

If you have unsaved changes open when applying IDs across scenes, the wizard will offer to save and continue in one click.

---

## Quick Usage Example

Readiness is checked internally, so you can call `Show...Ad` directly — no need to check `IsRewardedAdReady()` first:

```csharp
using UnityEngine;
using EasyAdMob;

public class ShopController : MonoBehaviour
{
    public void OnWatchRewardAd()
    {
        AdManager.Instance.ShowRewardedAd(() =>
        {
            // Reward code executes here on success
            Debug.Log("Granted 100 Gold Coins!");
        });
    }
}
```

### Handling a missing ad

Every `Show...Ad` method also accepts an optional `onAdUnavailable` callback, in case you want to react when nothing was loaded yet (a fresh load is always queued automatically either way, so a later attempt is more likely to succeed without you doing anything extra):

```csharp
AdManager.Instance.ShowRewardedAd(
    onRewardSuccess: () => Debug.Log("Granted 100 Gold Coins!"),
    onAdUnavailable: () => Debug.Log("No rewarded ad ready yet - try again in a moment."));

AdManager.Instance.ShowInterstitialAd(
    onAdUnavailable: () => Debug.Log("No interstitial ready."));

AdManager.Instance.ShowRewardedInterstitialAd(
    onRewardSuccess: () => Debug.Log("Granted bonus reward!"),
    onAdUnavailable: () => Debug.Log("No rewarded interstitial ready."));
```

### Checking readiness ahead of time

`IsRewardedAdReady()`, `IsRewardedInterstitialAdReady()`, `IsInterstitialAdReady()`, and `IsBannerAdReady()` remain public for cases where you want to know in advance — for example, to gray out a "Watch Ad" button while nothing is loaded:

```csharp
watchAdButton.interactable = AdManager.Instance.IsRewardedAdReady();
```

### Banners

Banners work a little differently since loading is asynchronous — there's nothing to show the instant you ask for it, so `ShowBannerAd()` starts a load and calls `onAdUnavailable` if nothing was ready yet, rather than showing anything immediately:

```csharp
AdManager.Instance.ShowBannerAd(onAdUnavailable: () => Debug.Log("Banner not loaded yet."));

// Or check first, e.g. before deciding to reserve screen space for it:
if (AdManager.Instance.IsBannerAdReady())
{
    AdManager.Instance.ShowBannerAd();
}
```

If any ad fails to load or fails to actually display once shown, `AdManager` logs a warning with the underlying error message and automatically queues a fresh load, so you don't need to handle retries yourself.

---

## Full API Reference

Everything below is called as `AdManager.Instance.MethodName(...)`.

### Banner

| Member | Description |
| --- | --- |
| `void LoadBannerAd()` | Loads (or reloads) a banner ad in the background. Called automatically on start and whenever needed, but safe to call manually to pre-load early. |
| `bool IsBannerAdReady()` | Returns `true` if a banner has finished loading and is ready to show. |
| `void ShowBannerAd(Action onAdUnavailable = null)` | Shows the loaded banner. If none is ready yet, starts loading one, invokes `onAdUnavailable` if provided, and does not show anything this call. |
| `void HideBannerAd()` | Hides the banner without destroying it — it can be shown again later with `ShowBannerAd()`. |
| `void DestroyBannerAd()` | Destroys the current banner entirely. Call `LoadBannerAd()` again to create a new one. |
| `static event Action OnBannerAdLoaded` | Fires whenever a new banner finishes loading and becomes available — subscribe instead of polling `IsBannerAdReady()` if you want to react the moment it's ready (e.g. to fade it in). |

### Interstitial

| Member | Description |
| --- | --- |
| `void LoadInterstitialAd()` | Loads (or reloads) an interstitial ad in the background. Called automatically on start and after each show, but safe to call manually. |
| `bool IsInterstitialAdReady()` | Returns `true` if an interstitial is loaded and ready to show. |
| `void ShowInterstitialAd(Action onAdUnavailable = null)` | Shows the interstitial if ready. If not, starts loading one and invokes `onAdUnavailable` if provided. |
| `static event Action OnInterstitialAdLoaded` | Fires whenever a new interstitial finishes loading and becomes available. |

### Rewarded

| Member | Description |
| --- | --- |
| `void LoadRewardedAd()` | Loads (or reloads) a rewarded ad in the background. Called automatically on start and after each show, but safe to call manually. |
| `bool IsRewardedAdReady()` | Returns `true` if a rewarded ad is loaded and ready to show. |
| `void ShowRewardedAd(Action onRewardSuccess, Action onAdUnavailable = null)` | Shows the rewarded ad if ready and invokes `onRewardSuccess` once the player has earned the reward **and** closed the ad. If not ready, invokes `onAdUnavailable` instead and starts loading a new one. |
| `static event Action OnRewardedAdLoaded` | Fires whenever a new rewarded ad finishes loading and becomes available — subscribe to this if you want to react to ad availability changing anywhere in your codebase, rather than polling `IsRewardedAdReady()`. |

### Rewarded Interstitial

| Member | Description |
| --- | --- |
| `void LoadRewardedInterstitialAd()` | Loads (or reloads) a rewarded interstitial ad in the background. Called automatically on start and after each show, but safe to call manually. |
| `bool IsRewardedInterstitialAdReady()` | Returns `true` if a rewarded interstitial is loaded and ready to show. |
| `void ShowRewardedInterstitialAd(Action onRewardSuccess, Action onAdUnavailable = null)` | Shows the ad if ready and invokes `onRewardSuccess` once the player has earned the reward **and** closed the ad. If not ready, invokes `onAdUnavailable` instead and starts loading a new one. |
| `void ShowRewardedInterstitialAd()` | Convenience overload — same as above with no callbacks, for cases where you don't need to know the outcome. |
| `static event Action OnRewardedInterstitialAdLoaded` | Fires whenever a new rewarded interstitial finishes loading and becomes available. |

### General

| Member | Description |
| --- | --- |
| `static AdManager Instance` | The singleton instance, available anywhere after the `AdManager` GameObject has run `Awake()`. Always use this instead of tracking your own reference. |

**Notes on rewards:** the reward callback fires only after the ad has actually closed, not the moment the reward is technically earned — this avoids granting rewards, or changing game state, while the ad is still on screen. **Notes on failures:** every `Load...Ad` and `Show...Ad` method logs a warning with the underlying error message if something goes wrong (failed to load, failed to display), and automatically queues a fresh load — you generally don't need to write your own retry logic.

---

## License

Distributed under the Apache License 2.0. See `LICENSE` for details.