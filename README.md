<div align="center">

# 🎥 XRCap (VR180 & 2D Capture Suite)

**为终极沉浸感而生：游戏内离线逐帧录制与输出引擎**

[![Release](https://img.shields.io/badge/Release-v1.0.0-0078D7?style=for-the-badge)]()
[![Platform](https://img.shields.io/badge/Platform-Unity-black?style=for-the-badge&logo=unity)]()
[![License](https://img.shields.io/badge/License-Free_Lite-4CAF50?style=for-the-badge)]()
[![Get Pro](https://img.shields.io/badge/UPGRADE-GET_PRO-FF424D?style=for-the-badge&logo=payhip)](https://payhip.com/XRCap)

</div>

---

## 💡 简介 (Introduction)

**XRCap** 是一款深入游戏渲染管线底层的**离线逐帧录制器**，专门为 **VR180** 格式的高分辨率视频录制进行了深度优化。

传统的录制软件（如 OBS）会因为显卡渲染性能不足导致掉帧、卡顿。而 XRCap 接管了游戏的物理引擎与渲染时间轴，采用**离线逐帧（Offline Frame-by-Frame）**技术，无论你的显卡配置如何，都能输出**完美流畅、零掉帧、完美音画同步**的极清视频。

同时，XRCap 也完美支持常规的 2D 高画质视频录制、超采样（SSAA）截图以及 3D 空间音频捕获。

## 🎮 支持的游戏 (Supported Games)

正在持续适配更多基于 Unity 引擎开发的游戏。目前已完美支持：

* ✅ **深海迷航 (Subnautica)**
* ✅ **深海迷航：冰点之下 (Subnautica: Below Zero)**
* ✅ **超越蔚蓝 (Beyond Blue)**
* ✅ **HoneySelect 2 (HS2)**
* ⏳ *更多游戏适配中...*

---

## 🔥 核心特性 (Key Features)

* **极致的 VR180 渲染**：支持 Cubemap（完美 180° 无死角覆盖）与 Foveated（注视点分环渲染）模式；支持超级 ERP（折叠）投影输出。
* **8K 120FPS 10Bit录制**：支持高达 2x 的空间超采样（SSAA）抗锯齿，可直出 4:4:4 无损高保真 PNG，保留每一丝毛发细节。
* **3D 空间音频捕获**：内置3D虚拟音频引擎（Virtual Audio Engine），在逐帧慢速渲染时也能抓取并合成随镜头移动的 3D 立体音效。
* **VR防崩溃系统 (OOM Protection)**：内置 GPU Fence 显存防爆机制与 TDR 超时保护。当检测到显存枯竭时，自动安全排空管线并保存视频，拒绝闪退做白工。
* **Nvenc显存直通**：在没有场景瓶颈的情况下，编码速度对比FFmepg最高可提升3倍。
* **强大的 Mod 兼容性**：底层完美兼容并热修复第三方画质插件（兼容graphics、dhh等画质插件，针对MMDD适配）。

* **🤖 自动化与极简操作**
  * **MMDD 智能联动**：自动检测 MMD Director，悬停一键“开始录制 MMDD”，录制开始自动播放，结束自动停止。
---

## 💎 版本对比：2D普通版 vs 完全版

XRCap 普通版和完全版核心录制功能**完全免费**。

但如果您追求**视频极致的录制速度**与**突破极限的画质**，欢迎支持我们的项目，升级至 **XRCap PRO**！

| 功能特性 | 🟢 普通版（只包含2D）  | 👑 完全版 (包含VR+完整2D) |
| :--- | :---: | :---: |
| 2D 视频 / 截图 / 音频捕获 | ✔️ 完美支持 | ✔️ 完美支持 |
| VR180 全景视频录制 | ❌ 仅2D平面 | ✔️ 完美支持 |
| **视频导出最大分辨率** | 2D视频无任何限制 | 解锁上限，VR支持 **8K 及以上** |
| **NVENC 显存直通 (Zero-Copy)** | ❌ 仅支持常规 FFmpeg 导出 | ✔️ **开启** (编码速度提升 300%+) |
| **超级ERP/ Bloom 动态补偿** | ❌ 关闭 | ✔️ **开启** (超广角边缘无割裂感) |

#### 🚀 为什么选择 PRO 版？(关于 NVENC 显存直通)
在常规模式下，录制的高清画面需要从 GPU 显存回读到 CPU 内存，再交给 FFmpeg 编码，这会造成极大的性能瓶颈。

**PRO 版专属的 NVENC 直通模式** 直接在 GPU 内部完成画面渲染、投屏拼接与硬件编码，全程不经过内存！录制速度实现质的飞跃！

<div align="center">
  <br>
  <a href="https://payhip.com/XRCap">
    <img src="https://img.shields.io/badge/👉_立即获取_XRCap_PRO-FF424D?style=for-the-badge&logo=payhip" alt="Get Pro">
  </a>
  <p><i>（包含终身更新支持）</i></p>
</div>

---

## 📥 安装说明 (Installation)

1. 确保游戏已安装 [BepInEx](https://github.com/BepInEx/BepInEx) 框架（版本 5.4.22以上）。
2. 在 Release 页面下载最新的 `XRCap文件`。
3. 将解压后的 `完整文件夹` 放入游戏根目录 覆盖进 `BepInEx` 目录下。
4. 启动游戏，按下快捷键 `F6` 呼出控制面板，或点击左侧图标 <img src="./Resource/xrcap_icon_off.png" width="32" align="center">
5. 若呼不出控制面板，请到`F1`的bepinx配置面板找到 `XRCap`，将10. 快捷键 (Hotkeys)"的"Key Toggle GUI"改为未被使用的热键再重新尝试。


## ⚙️ 快速上手 (Quick Start)

* **呼出菜单**：按 `F6` 切换界面与预览框。
* **快捷截图**：设置好参数后，可绑定快捷键一键截取无损超采样画面。
* **开始录制**：在面板中调整好“分辨率”、“编码器”、“输出格式”，点击 **REC** 即可开始。
* 游戏会自动变慢进入逐帧渲染状态，游戏画面将实时显示渲染后的画面。

---

<div align="center">
  <p>Developed with ❤️ by liu85</p>
  <p>如果您觉得这个项目有帮助，欢迎点击右上角的 ⭐ Star！</p>
</div>
