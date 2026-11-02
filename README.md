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

To turn it on: Settings → Falling → Play Style, choose "Falling".

- **Display**
  - The 8 lanes are centered and take up 70% of the screen width on a 16:9 screen. The judge line spans the whole screen and the background fills it.
  - The game runs in landscape with the HUD in the corners: pause button at the top left (double-tap to pause), score at the top right, and below it the achievement rate counting down from 101%. Combo is at the top center, the song title at the bottom left, and the difficulty at the bottom right.
  - During the opening and closing transitions, everything outside the circle is covered.
- **Tap / Hold / Break**
  - Notes fall in the lane with the same number. Notes in the center C area fall between lanes 4 and 5, and either lane can hit them.
  - Each lane's judge range is the whole lane plus half a lane on each side. One press hits one note: the one closest to its judge time.
  - Mine Taps and mine stars only count as hit by a new press in their own lane near the judge line: sweeps, the neighbouring half lanes and Vertical Judge don't apply. A mine star is judged by its head only; its arrows are never judged.
- **Slides (stars)**
  - The original slide arrows are laid out along the path of the judge areas and fall with the notes.
  - Judging works like a Drag: it's a hit when a finger's horizontal position is within two lanes of the arrow on either side (four lanes in total). The jump between 8 and 1 (the two ends of the lanes) is judged more leniently.
  - Grades: every point hit is Critical Perfect, first and last hit is Perfect, first hit plus any other is Great, any hit is Good, and nothing hit is Miss.
- **Floating Touch**
  - Touch notes hover above their lane, from low to high in the order A, D, E, B, C.
  - The hit range extends one extra lane on each side. A finger entering or resting in the range counts as a hit. A finger already resting there is judged at the exact judge time.
  - Mine Touches use the original judging: only pressing or sliding into the Touch itself (its own lane, about the size of the graphic) during the 150 ms before it sets it off. A finger already resting there doesn't.
- **Scores**: Falling mode scores are saved locally, separately from normal scores, and are not uploaded. When falling mode is the only mod on, the result screen saves the score as usual. Options that change the chart (Touch to Tap, Slide Tail to Flick, Keep Slides Original) count as mods: scores are not saved while any of them is on. Options that only change the feel (Vertical Judge, Touch Vertical Judge, Sweep Taps, Break to Flick, and so on) don't affect saving.

#### Falling settings (Settings → Falling)

Every falling-mode setting is in its own **Falling** category and is saved with your settings, so nothing resets when the game restarts. Settings from older versions (in Game Settings and Mod) carry over automatically.

| Setting | Description |
|---|---|
| Play Style | Standard or Falling. |
| Note Speed | How fast notes fall. The value means the same as Tap speed. |
| Vertical Judge | On: a press at any height in the right lane counts for Tap, Hold, Break and slides. Off: the press has to be near the judge line. |
| Touch Vertical Judge | Off by default. A floating Touch can be hit at any height in its range (its lane plus one on each side). Independent of Vertical Judge; mine Touches are not affected. |
| Sweep Taps | A held finger sliding into a lane also counts as a tap, so you can sweep fast staircases. A finger following a slide doesn't sweep. |
| Break to Flick | A Break is judged only when you flick on the lane. |
| Slide Tail to Flick | Off by default. Slide arrows are replaced by one red Flick that reaches the judge line in the lane where the slide starts, at the moment it starts moving. Flick there to hit it; it's graded by Tap timing and counts toward Fast/Late. No effect when Keep Slides Original is on. Scores are not saved while this is on. |
| Touch to Tap | Touch and TouchHold notes become Tap and Hold notes falling in their lane. Scores are not saved while this is on. |
| Keep Slides Original | Off by default. Stars play as in standard mode: star heads go back to the outer ring and are judged there, and slides float in the middle of the screen, looking and judged as in standard play. Scores are not saved while this is on. |
| Lane Dividers | Show the vertical lines between lanes. |
| Light Up Touched Lanes | Off by default; only works while Lane Dividers are shown. The lane under your finger lights up and follows your finger, whether or not Sweep Taps is on. |
| Follow Info Display | Off: use the HUD layout above. On: combo and other info follow the display settings. |
| Difficulty Color / Score Color | Color the difficulty (bottom right) and the score (top right) by grade. |
| Judge Line Color | White; maimai (gold for AP, purple for Great, green for Good, white for Miss); or Phigros (gold at ≥100.5%, blue at ≥100%, otherwise white). |

### Landscape mode

Settings → Display Settings → Screen Orientation, choose "Landscape". Every page is shown in landscape: the main screen is centered, the sub screen is shrunk into the top left corner, and the rest is covered.

### Scores (Settings → Scores)

| Setting | Description |
|---|---|
| Score Set | Follow Play Style (default), Standard or Falling. |
| Share Range | Current Chart (the chart selected on the song list before opening settings), Best, All, or Selected (the charts you picked on the list-style song list). Each difficulty of a song is its own entry. |
| Share Count | How many to share for Best (5–200, default 50). |
| Share Sort Order | How Best and All are sorted: difficulty level (e.g. 14+ > 14 > 13+; same level: Re:MASTER > MASTER > …), achievement and DX score, compared in any of the 6 possible orders, all from high to low (default: level → achievement → DX). Best is sorted first, then the top ones are taken. |
| Selected Charts Order | Order of the selected charts: the order you picked them in (default), or the Share Sort Order. |
| Share Format | Image, Text, Spreadsheet (CSV) or Data (JSON). The image shows your avatar and nickname at the top left (from the logged-in account, otherwise "Guest") and "Best X" or "All N" with the date at the top right, then score cards with cover art, achievement, grade, FC/AP, DX score and play count ("3 PC"), up to 50 per image. Text, CSV and JSON carry the same header (nickname, Best X / All N, date) and DX score and play count for every entry; files are named like `Nickname-Best50-2026-11-01.csv`. |
| Share | Press + or − on this row to open the system share sheet (iOS/Android). Share, Export Backup and Import Backup show "Done" or "Failed" for a few seconds, then return to "Press +". |
| Export Backup | Exports every standard and falling score to the `ScoreBackups` folder (Files app on iOS, `Documents/MajdataPlay/ScoreBackups` on Android). |
| Backup File / Import Backup | Pick a file in `ScoreBackups` and import it. Scores are merged: for each chart the better achievement, DX score and FC/AP are kept. Older score JSON files are accepted too. |

### Other

- **List-style song select**: Settings → Game Settings → Song List Style, choose "List" (default is the original cover carousel). A touch-friendly page:
  - Categories take the left quarter of the screen; on the right, one song card per row with the cover, title (scrolls when too long), artist and charter, length and BPM (plus plays, likes and comments for online charts), difficulty tags with full names and each difficulty's achievement underneath, a heart to favorite, and the best score with a colored grade, FC/AP, DX score and "X PC · last played".
  - The top bar reads "Cover  −  difficulty  +  Settings". "−" / "+" switch the global difficulty without losing your scroll position; a draggable scrollbar on the right; a floating pager at the bottom. Difficulty and pages wrap around at either end.
  - "Select to share" (next to the category name) enters selection mode: tap difficulty tags to pick or unpick those charts (picked tags are filled with the difficulty color). Picks are kept across pages and categories and saved with your settings. A floating bar shows "N selected · Clear · Share"; sharing uses the Selected Charts Order. The detail page also has an "Select / Unselect" button for the shown difficulty.
  - The detail page shows the same "Cover  −  difficulty  +  Settings" controls at the top right; there "−" / "+" only cycle through the difficulties this song has, without changing the global difficulty. Opening settings from the detail page brings you back to it. Songs per page is a setting (Game Settings → Songs per List Page, default 20).
  - Tap a card for chart details: big cover with a heart, share/export buttons and an info card on the left; title, difficulty bar, score card, chart radar and note density on the right; "Back" at the top left and a floating "Play" button at the bottom right. Opening and closing details slides and fades, and the preview and background music fade in and out.
  - Pressing Play shows a new loading screen (cover, difficulty, title, artist and charter, best score and loading progress) that fades out as the game opens. After playing a chart started from its detail page, you come back to that detail page, with the list where you left it.
  - The list fades out and in together with the game's scene transition when entering settings or coming back.
  - The category sidebar starts with a title bar (app icon, "MajdataPlay", "urntt edition"); tap the icon to open this repository on GitHub. All cover art has rounded, outlined corners, including the share image.
- **New settings and result screens** (only when Song List Style is "List"):
  - **Settings**: categories on the left (chart settings first, with the current song's title), one card per option on the right with its name, the full description and the value between "−" and "+" (hold to repeat). "Calibrate" and "Done" at the top right. Cabinet buttons still work: A2/A7 switch categories, A3/A6 select options, the touch sensors change values, A4/A5 leave and A1 opens calibration. Tap the "Render Quality" card 10 times (or press A8 10 times on it) to unlock custom rendering.
  - **Result**: rounded cover with a favorite heart, difficulty, title, artist and charter; like and upload buttons for online charts; the achievement rolls up next to its grade and FC/AP, compared with your previous best; DX score with stars; a judgement table per note type (TAP, HOLD, SLIDE, TOUCH, BREAK and total); FAST/LATE, average offset and the timing distribution. "Continue" becomes available once the score is saved. "Share score" shares this play (image, text, CSV or JSON), and "Classic view" switches back to the original result screen for this result. Dan and practice keep the original screen.
  - "Cover" switches back to the carousel for this visit.
- **Instant Song Preview**: Settings → Game Settings, off by default. When on, switching songs on the song list plays the new preview right away instead of returning to the background music in between.
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

- Unity Editor (version specified in ProjectSettings/ProjectVersion.txt), with Android Build Support (SDK API 36 and OpenJDK) for Java-wrapper generation, including desktop Editor imports

- .NET 9 SDK for the independent Android Java wrapper CLI (not for building the Unity project)

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

### Generate the Android Java Bindings

The storage backend uses a locally compiled Android Java wrapper DLL.
Generate them with the independent CLI before the first Unity import:

```powershell
Push-Location Tools/CSharp/AndroidJavaWrapperGenerator
dotnet run --project AndroidJavaWrapperGenerator.csproj -- --project ../../.. --unity-data "C:/Program Files/Unity Editors/6000.3.17f1/Editor/Data" --no-docs
Pop-Location
```

This builds the independent generator and `Tools/AndroidJavaWrapper` library;
Unity-generated projects are built only by Unity.
Pass another `--unity-data` directory for a different installation and `--sdk`
for a separately configured Android SDK. Fresh checkouts and CI need the same
binding generation step. Sources are generated into `Tools/AndroidJavaWrapper/src`,
alongside tracked `.e.cs` partial extensions. The standalone project compiles
`MajdataPlay.Platform.Android.Runtime.dll` and installs it into
`Assets/Plugins/MajdataPlay/Platform/Android` only after successful compilation.
The project no longer requires the legacy Roslyn analyzer.

The Editor command **Tools > MajdataPlay > Android > Regenerate Java wrappers**
uses the independent `Tools/CSharp/AndroidJavaWrapperGenerator` CLI to generate
the complete SDK/BCL plus Java inputs from `Assets/Plugins/Android` and the current
Unity Android player. It runs in the background with official documentation and
compiles and installs the wrapper DLL. Helpers use
`MajdataPlay.Platform.Android.Runtime`; bindings use its `Bindings` namespace.
Automatic Java asset refreshes reuse cached pages and fetch missing documentation. See
`Assets/Plugins/MajdataPlay/Platform/Android/README.md` for output ownership,
cancellation and setup. See `Tools/AndroidJavaWrapper/README.md` for partial
extensions and rebuilding the DLL without regenerating Java bindings.

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
