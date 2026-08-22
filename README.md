# EasyAdMob for Unity

A lightweight, plug-and-play Unity Package Manager (UPM) wrapper for **Google Mobile Ads (AdMob)**. Designed to eliminate boilerplate code and streamline setup across your Unity games with standard AdMob formats (Adaptive Banners, Interstitials, Rewarded, and Rewarded Interstitials).

---

## Features

* **One-Click Scene Setup:** Custom Unity Editor wizard (`Tools > EasyAdMob > Setup Wizard`) to instantiate and configure `AdManager` instantly.
* **Auto Showcase Scene:** Generates an isolated UI demo scene equipped with pre-wired buttons to test all ad formats right out of the box.
* **Dependency Automation:** Built-in SDK downloader directly inside Unity Editor.
* **Clean Singleton Architecture:** Call ads anywhere in your codebase with `using EasyAdMob;`.
* **Delegate Rewards:** Lambda/action callbacks to grant in-game items upon ad completion.

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

1. Open the Setup Wizard via **Tools > EasyAdMob > Setup Wizard**.
2. Click **Download & Install Google Mobile Ads** to automatically fetch dependencies.
3. Configure your Google Mobile Ads App IDs & Ad Unit IDs (or click **Fill Test IDs** to quickly populate Google's official test units).
4. Click **Apply IDs to Project & Scene**.
5. Click **Generate Showcase Demo Scene** to instantly test all ad implementations.

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

Distributed under the Apache License 2.0. See `LICENSE` for details.