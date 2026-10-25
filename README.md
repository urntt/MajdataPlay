# MajdataPlay

![license GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue)
![GitHub Release](https://img.shields.io/github/v/release/LingFeng-bbben/MajdataPlay)
![Discord](https://badgen.net/discord/online-members/AcWgZN7j6K)
![Unity](https://img.shields.io/badge/Unity-6000.3.17f1-7e7e7e)

> [!NOTE]
> This software has no affair with the `big S four letter` company, please support the arcade whenever you can.

A Simai Player.

This project is based on [@LeZi9916](https://github.com/LeZi9916) 's DJAuto branch for [MajdataView](https://github.com/LingFeng-bbben/MajdataView).

Simai is a maimai chart discription language developed by [Celeca](https://twitter.com/formiku39854)

## 本 Fork 新增内容

这是 [LingFeng-bbben/MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的 Fork，在原版基础上新增了 Phigros 式的**下落式玩法**和**横屏模式**。安装包见 [Releases](https://github.com/urntt/MajdataPlay/releases)，提供未签名的 iOS ipa 和 Android APK。

### 下落式玩法

开启方式：设置 → Mod → 游玩模式，选“下落式”。

- **画面**
  - 8 条轨道居中，在 16:9 屏幕上占屏宽的 70%，判定线横贯整个屏幕，背景铺满屏幕。
  - 横屏显示，HUD 在四角：左上暂停键（双击暂停），右上分数，下方是从 101% 往下掉的达成率；顶部中间连击，左下曲名，右下难度。
  - 开局和结束的转场动画期间，圆形以外的区域会遮黑。
- **Tap / Hold / Break**
  - 落在对应编号的列上；中心 C 区的音符落在第 4、5 列之间，按任一列都算。
  - 每列的判定范围是整列加左右各半列。一次按下只打一个音符，给离判定时刻最近的那一个。
- **星星（Slide）**
  - 原版的 Slide 箭头沿判定区路径排布，随音符一起下落。
  - 判定是 Drag 式：手指的横坐标离箭头不超过一列半就算命中。经过 8 与 1 之间（轨道两端）的跳跃时会放宽。
  - 评级：全中 Critical Perfect，首尾都中 Perfect，首中且另有命中 Great，有命中 Good，全漏 Miss。
- **悬空 Touch**
  - 按 A、D、E、B、C 圈由低到高，悬停在对应列的上方。
  - 可按范围是左右各多一列；手指进入或停留在范围内就算命中，提前停着的手指会等到判定时刻再判定。
- **成绩**：下落式的成绩单独保存在本地，与普通成绩分开，不上传；只开下落式时，结算页照常保存成绩。

#### 下落式设置（设置 → 游戏设定，随设置保存）

| 设置 | 说明 |
|---|---|
| 下落式速度 | 音符下落快慢，数值含义与 Tap 速度相同 |
| 下落式轨道分割线 | 是否显示轨道之间的竖线 |
| 下落式扫键 | 按住的手指划进某一列也算点击，可以扫连续的台阶；正在跟星星的手指不扫键 |
| 下落式信息跟随显示设置 | 关闭时用上面的 HUD 布局；开启时连击等信息按显示设置 |
| 下落式难度颜色 / 分数颜色 | 右下难度、右上分数是否按评级上色 |
| 下落式判定线颜色 | 纯白；maimai 式（AP 金、Great 紫、Good 绿、Miss 白）；Phigros 式（≥100.5% 金、≥100% 蓝，否则白） |

#### 下落式 Mod（设置 → Mod）

| Mod | 说明 |
|---|---|
| 下落式垂直判定 | 开启时在正确的列上任意高度都算，关闭时需按在判定线附近 |
| 下落式 Break 转 Flick | Break 需要在轨道上划动才会判定 |
| 下落式 Touch 转 Tap | Touch、TouchHold 改为在对应列下落的 Tap、Hold |
| 下落式 Slide 保持原样 | 星星保持标准游玩的样式和判定，悬空显示在画面中央 |

### 横屏模式

设置 → 显示设定 → 画面方向，选横屏后所有页面都横屏显示：主屏居中，副屏缩小放在左上角，其余区域遮黑。

### 其他

- **保存 Mod 设置**：设置 → 调试，开启后 Mod 设置随设置一起保存，重启游戏不会重置。
- **应用图标**：加了一个粉色的 U 字发卡。
- **打包**
  - `Tools/iOS/build_unsigned_ipa.sh`：在 Mac 上一条命令打出未签名 ipa。
  - GitHub Actions：提交信息带 `[ipa]` 打 iOS 包，带 `[apk]` 打 Android 包，带 `[release]` 同时打两个并发布 Release。
  - 说明见 [Tools/iOS/README.md](Tools/iOS/README.md)。
- **Android 安装包**：包名为 `com.<仓库所有者>.majdataplay`，可与官方版同时安装，谱面目录与官方版相同（`/sdcard/Documents/MajdataPlay`）。
  - 在仓库 Secrets 里配置 `ANDROID_KEYSTORE_BASE64`、`ANDROID_KEYSTORE_PASS`、`ANDROID_KEYALIAS_NAME`、`ANDROID_KEYALIAS_PASS` 后，每次用同一个签名，可以直接覆盖更新。
  - 不配置时每次临时生成签名，更新前需要先卸载旧版。

## Supported platforms

- Windows
- [Linux](https://github.com/LingFeng-bbben/MajdataPlay/wiki/%E5%B9%B3%E5%8F%B0%E7%9B%B8%E5%85%B3#linux) (Partially Supported)
- macOS
- Android
- iOS

> [!Note]
> On macOS, only the DAO device is tested and fully supported. Other devices are untested.

## Install

# !DOWNLOAD NOW

## !DOWNLOAD NOW

### !DOWNLOAD NOW

#### !DOWNLOAD NOW

##### !DOWNLOAD NOW

[<img src="Doc/googleplay.svg" height="60">](https://play.google.com/store/apps/details?id=net.majdata.majdataplay) [<img src="Doc/appstore.svg" height="60">](https://testflight.apple.com/join/PwxCNk5n)

## Getting Started

> [!WARNING]
> This repository contains a Unity project, not a standard C#/.NET project.
>
> Do not open or build it using dotnet, Visual Studio solution files, or other .NET build tools.
>
> The project must be opened using the Unity Editor.

### Requirements

- Unity Editor (version specified in ProjectSettings/ProjectVersion.txt)

- git

- Unity Hub

### Clone the Repository

Clone the project using Git:

```bash
git clone https://github.com/LingFeng-bbben/MajdataPlay.git MajdataPlay
cd MajdataPlay
```

### Initialize Submodules

This project uses Git submodules. After cloning, run:

```bash
git submodule update --init --recursive
```

If you already cloned the repository without submodules, you can also run:

```bash
git submodule sync
git submodule update --init --recursive
```

### Install the Required Unity Version

This project must be opened with the Unity version specified in:

```text
ProjectSettings/ProjectVersion.txt
```

Example:

```text
m_EditorVersion: 6000.3.17f1
```

Install this version using Unity Hub.

### Open the Project in Unity Hub

1. Open Unity Hub

2. Click Add Project

3. Select the cloned project folder

4. Ensure the correct Unity version is selected

5. Open the project

## Guidance

[Docs](https://docs.majdata.net) | [WIKI](https://github.com/LingFeng-bbben/MajdataPlay/wiki)

## Releases

[Stable](https://github.com/TeamMajdata/MajdataPlay_Build) | [Nightly](https://github.com/LingFeng-bbben/MajdataPlay/releases/tag/nightly)

## Reporting Problems

Feel free if you wanna participate in coding or testing!!

Please report problems to [issues page](https://github.com/LingFeng-bbben/MajdataPlay/issues).

The log files should be in `Logs/`

## References

- [istareatscreens/MychIO](https://github.com/istareatscreens/MychIO)
  - Special thanks to istareatscreens for the early I/O solution for this project!
- [Cysharp/UniTask](https://github.com/Cysharp/UniTask)
- [Cysharp/ZString](https://github.com/Cysharp/ZString)
- [IntergatedCircuits/HidSharp](https://github.com/IntergatedCircuits/HidSharp)
- [ManagedBass/ManagedBass](https://github.com/ManagedBass/ManagedBass)
- [un4seen/bass](https://www.un4seen.com/)
- [mono/SkiaSharp](https://github.com/mono/SkiaSharp)
- [ammariqais/SkiaForUnity](https://github.com/ammariqais/SkiaForUnity)
- [videolan/vlc-unity](https://github.com/videolan/vlc-unity)
