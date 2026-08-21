# EasyAdMob for Unity

A lightweight, plug-and-play Unity Package Manager (UPM) wrapper for **Google Mobile Ads (AdMob)**. Designed to eliminate boilerplate code and streamline setup across your Unity games with standard AdMob formats (Adaptive Banners, Interstitials, Rewarded, and Rewarded Interstitials).

---

## Features

* **One-Click Scene Setup:** Custom Unity Editor wizard (`Tools > EasyAdMob > Setup Wizard`) to instantiate and configure `AdManager` instantly.
* **Dependency Automation:** Built-in SDK downloader directly inside Unity Editor.
* **Clean Singleton Architecture:** Call ads anywhere in your codebase using `using EasyAdMob;`.
* **Delegate Rewards:** Lambda/action callbacks to grant in-game items upon ad completion.
* **Safe Pre-compilation:** Guarantees zero project compilation errors if the AdMob SDK is not yet present.

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

## Quick Usage Example

```csharp
using UnityEngine;
using EasyAdMob;

public class ShopController : MonoBehaviour
{
    public void OnWatchRewardAd()
    {
        if (AdManager.Instance.IsRewardedAdReady())
        {
            AdManager.Instance.ShowRewardedAd(() =>
            {
                // Reward code executes here on success
                Debug.Log("Granted 100 Gold Coins!");
            });
        }
    }
}

```

---

## License

Distributed under the MIT License. See `LICENSE` for details.