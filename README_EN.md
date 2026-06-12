<div align="center">

# 🎥 XRCap (VR180 & 2D Capture Suite)

**Built for Ultimate Immersion: In-Game Offline Frame-by-Frame Recording & Output Engine**

[![Download](https://img.shields.io/badge/RELEASE-1.1.0-0078D7?style=for-the-badge&logo=github)](https://github.com/liu85mod/XRCap/releases)
[![Platform](https://img.shields.io/badge/Platform-Unity-4CAF50?style=for-the-badge&logo=unity)]()
[![Patreon](https://img.shields.io/badge/SUPPORT-PATREON-FF424D?style=for-the-badge&logo=patreon)](https://www.patreon.com/liu85)
[![Payhip](https://img.shields.io/badge/UPGRADE-GET_PRO-FFB800?style=for-the-badge&logo=payhip)](https://payhip.com/XRCap)

</div>

---

## 💡 Introduction

**XRCap** is a low-level **offline frame-by-frame recorder** that hooks directly into the game's rendering pipeline. It is deeply optimized for capturing high-resolution video in **VR180** format.

Traditional capture software (like OBS) suffers from dropped frames and stuttering when the GPU hits rendering bottlenecks. XRCap bypasses this by taking complete control of the game's physics engine and rendering timeline via **Offline Frame-by-Frame** rendering. No matter your hardware specs, it guarantees **perfectly smooth, zero-dropped-frame, and pixel-perfect audio-video synchronized** ultra-high-definition exports.

Additionally, XRCap natively supports standard high-fidelity 2D video recording, supersampled (SSAA) screenshots, and 3D spatial audio capture.

---

## 📥 Download

> ### 🚀 [Click here to visit the Releases page and download the latest version](https://github.com/liu85mod/XRCap/releases)

---

## 🎮 Supported Games

Actively expanding compatibility for Unity-driven titles. The following are fully supported out of the box:

* ✅ **Subnautica**
* ✅ **Subnautica: Below Zero**
* ✅ **Beyond Blue**
* ✅ **HoneySelect 2 (HS2)**
* ⏳ *More titles coming soon...*

---

## 🔥 Key Features

* **Ultimate VR180 Rendering**: Supports Cubemap (flawless 180° seamless coverage) and Foveated (foveated multi-ring rendering) modes; supports ultra-ERP (folded) projection exports.
* **8K 120FPS 10-Bit Capture**: Supports up to 2x spatial supersampling (SSAA) anti-aliasing. Directly exports 4:4:4 lossless high-fidelity PNG sequences, preserving every microscopic strand of hair and texture detail.
* **3D Spatial Audio Capture**: Features an integrated 3D Virtual Audio Engine. Even during slow, frame-by-frame offline rendering, it accurately captures and synthesizes 3D spatial audio that tracks perfectly with camera movement.
* **Crash Prevention System (OOM Protection)**: Built-in GPU Fence VRAM overflow prevention and TDR (Timeout Detection and Recovery) delay management. When VRAM exhaustion is imminent, the engine automatically and safely flushes the pipeline and preserves the recorded segment—preventing sudden crashes and lost work.
* **Zero-Copy NVENC Pipeline**: Achieves up to a 3x encoding speedup compared to standard FFmpeg pipelines when free from scene geometry bottlenecks.
* **Robust Mod Ecosystem Compatibility**: Low-level integration that provides seamless hotfixes and compatibility for third-party graphics and post-processing mods (fully compatible with Graphics, DHH, etc.; explicitly optimized for MMD workflows).
* **🤖 Automation & Streamlined Operations**
  * **MMDD Smart Linkage**: Automatically detects MMD Director. Trigger "Start MMDD Recording" to auto-play on start and auto-stop once the sequence concludes.

---

## 💎 Edition Comparison: 2D vs. Full Feature

The core recording features of both XRCap 2D and the Full Edition are **completely free**.

If you require **blazing-fast encoding speeds** and **boundary-pushing visual fidelity**, consider supporting the project by upgrading to **XRCap PRO**!

| Feature / Capability | 🟢 2D Edition | 👑 Full Edition (VR + Complete 2D) |
| :--- | :---: | :---: |
| **2D Video / Screenshot / Audio Capture** | ✔️ Fully Supported | ✔️ Fully Supported |
| **VR180 Panoramic Video Recording** | ❌ 2D Flat Plane Only | ✔️ Fully Supported |
| **Export Framerate & Resolution** | ✔️ No limits for 2D video | ✔️ Uncapped (Supports **8K and beyond** for VR) |
| **Zero-Copy NVENC Pipeline** | ❌ Standard FFmpeg export only | ✔️ **Enabled** (300%+ speedup) |
| **Ultra-ERP / Bloom Dynamic Compensation** | ❌ Not required | ✔️ **Enabled** (Eliminates edge separation in ultra-wide FOV) |
| **OOM Crash Prevention System** | ❌ Stripped (May crash under high loads) | ✔️ **Included** (Auto-detects and prevents crashes/VRAM overflow) |

#### 🚀 Why go PRO? (Understanding the Zero-Copy NVENC Pipeline)
In standard capture modes, high-definition frames must be read back from GPU VRAM to CPU system memory before being passed to FFmpeg for encoding, creating a severe hardware bottleneck.

The **PRO-exclusive NVENC Direct Pass pipeline** handles frame rendering, projection stitching, and hardware encoding entirely within GPU memory—completely bypassing system RAM. This delivers a massive leap in capture throughput.

---

## 📥 Installation

1. Ensure your game has the [BepInEx](https://github.com/BepInEx/BepInEx) modding framework installed (version 5.4.22 or higher).
2. Download the latest `XRCap` archive from the **[Releases Page](https://github.com/liu85mod/XRCap/releases)**.
   * `XRCap_HS2_2D`: Contains standard 2D recording features only.
   * `XRCap_HS2_FULL`: Full-featured suite containing all 2D and VR capabilities.
3. Extract the contents and drop the folder into your game's root directory, merging it directly into the `BepInEx` folder.
4. Launch the game and press the default hotkey `F6` to toggle the control panel, or click the icon on the left: <img src="./Resource/xrcap_icon_off.png" width="32" align="center">
5. If you experience hotkey conflicts or the GUI fails to appear, press `F1` in-game to open the BepInEx Configuration Manager, locate the `XRCap` section, and rebind `Key Toggle GUI` to an unassigned key.

## ⚙️ Quick Start

* **Toggle Menu**: Press `F6` to switch the visibility of the main dashboard and preview window.
* **Quick Screenshot**: Configure your parameters in the panel, then bind a hotkey to take instant, lossless supersampled (SSAA) captures.
* **Start Recording**: Adjust your "Resolution", "Encoder", and "Output Format" inside the dashboard, then click **Start Recording**.
* **Capture Status**: Once initiated, the game will automatically enter a frame-by-frame rendering state. The game viewport will display the rendered frames in real-time, accompanied by an On-Screen Display (OSD) showing current render progress.

---

<div align="center">
  <p>Developed with ❤️ by liu85</p>
  <p>If you find this project useful, please give it a ⭐ Star on GitHub!</p>
</div>
