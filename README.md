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

## What's New in This Fork

This is a fork of [LingFeng-bbben/MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) that adds a Phigros-style **falling mode** and a **landscape mode**. Builds are on the [Releases](https://github.com/urntt/MajdataPlay/releases) page: an unsigned iOS ipa and an Android APK.

### Falling mode

To turn it on: Settings → Mod → Play Style, choose "Falling".

- **Display**
  - The 8 lanes are centered and take up 70% of the screen width on a 16:9 screen. The judge line spans the whole screen and the background fills it.
  - The game runs in landscape with the HUD in the corners: pause button at the top left (double-tap to pause), score at the top right, and below it the achievement rate counting down from 101%. Combo is at the top center, the song title at the bottom left, and the difficulty at the bottom right.
  - During the opening and closing transitions, everything outside the circle is covered.
- **Tap / Hold / Break**
  - Notes fall in the lane with the same number. Notes in the center C area fall between lanes 4 and 5, and either lane can hit them.
  - Each lane's judge range is the whole lane plus half a lane on each side. One press hits one note: the one closest to its judge time.
- **Slides (stars)**
  - The original slide arrows are laid out along the path of the judge areas and fall with the notes.
  - Judging works like a Drag: it's a hit when a finger's horizontal position is within one and a half lanes of the arrow. The jump between 8 and 1 (the two ends of the lanes) is judged more leniently.
  - Grades: every point hit is Critical Perfect, first and last hit is Perfect, first hit plus any other is Great, any hit is Good, and nothing hit is Miss.
- **Floating Touch**
  - Touch notes hover above their lane, from low to high in the order A, D, E, B, C.
  - The hit range extends one extra lane on each side. A finger entering or resting in the range counts as a hit. A finger already resting there is judged at the exact judge time.
- **Scores**: Falling mode scores are saved locally, separately from normal scores, and are not uploaded. When falling mode is the only mod on, the result screen saves the score as usual.

#### Falling settings (Settings → Game Settings, saved with your settings)

| Setting | Description |
|---|---|
| Falling: Note Speed | How fast notes fall. The value means the same as Tap speed. |
| Falling: Lane Dividers | Show the vertical lines between lanes. |
| Falling: Sweep Taps | A held finger sliding into a lane also counts as a tap, so you can sweep fast staircases. A finger following a slide doesn't sweep. |
| Falling: Follow Info Display | Off: use the HUD layout above. On: combo and other info follow the display settings. |
| Falling: Difficulty Color / Score Color | Color the difficulty (bottom right) and the score (top right) by grade. |
| Falling: Judge Line Color | White; maimai (gold for AP, purple for Great, green for Good, white for Miss); or Phigros (gold at ≥100.5%, blue at ≥100%, otherwise white). |

#### Falling mods (Settings → Mod)

| Mod | Description |
|---|---|
| Falling: Vertical Judge | On: a press at any height in the right lane counts. Off: the press has to be near the judge line. |
| Falling: Break to Flick | A Break is judged only when you flick on the lane. |
| Falling: Touch to Tap | Touch and TouchHold notes become Tap and Hold notes falling in their lane. |
| Falling: Keep Slides Original | Off by default. Stars play as in standard mode: star heads go back to the outer ring and are judged there, and slides float in the middle of the screen, looking and judged as in standard play. |

### Landscape mode

Settings → Display Settings → Screen Orientation, choose "Landscape". Every page is shown in landscape: the main screen is centered, the sub screen is shrunk into the top left corner, and the rest is covered.

### Other

- **Save Mod Settings**: Settings → Debug. When on, Mod settings are saved with your settings and don't reset when the game restarts.
- **App icon**: now wears a pink U-shaped hairclip.
- **Packaging**
  - `Tools/iOS/build_unsigned_ipa.sh` builds an unsigned ipa on a Mac with one command.
  - GitHub Actions: a commit message containing `[ipa]` builds the iOS ipa, `[apk]` builds the Android APK, and `[release]` builds both and publishes a release.
  - See [Tools/iOS/README.md](Tools/iOS/README.md) for details.
- **Android APK**: the package name is `com.<repository owner>.majdataplay`, so it installs alongside the official build. It uses the same chart folder as the official build (`/sdcard/Documents/MajdataPlay`).
  - With `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME` and `ANDROID_KEYALIAS_PASS` set in the repository secrets, every build uses the same signing key and can update in place.
  - Without them, each build uses a temporary key, so uninstall the old version before updating.

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
