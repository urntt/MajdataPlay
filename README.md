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

This is a fork of [LingFeng-bbben/MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) that adds a **falling mode** (notes fall along 8 lanes to a judge line) and a **landscape mode**. Builds are on the [Releases](https://github.com/urntt/MajdataPlay/releases) page: an unsigned iOS ipa and an Android APK.

### Falling mode

To turn it on: Settings → Falling → Play Style, choose "Falling".

- **Display**
  - The 8 lanes are centered and take up 85% of the screen width inside the safe area (the notch, rounded corners and home indicator are left out). On 4:3 screens they keep their old width; on 16:9 screens they grow from 70% to 85% of the screen, and on phones in landscape they are about 1.3 times as wide as before. The width is set when a game starts. The judge line spans the whole screen and the background fills it.
  - The game runs in landscape with the HUD in the corners: pause button at the top left (double-tap to pause), score at the top right, and below it the achievement rate counting down from 101%. Combo is at the top center, the song title at the bottom left, and the difficulty at the bottom right. The corner items stay inside the safe area and move when the device is turned around; on phones they and the pause menu are drawn larger, like the other touch screens.
  - During the opening and closing transitions, everything outside the circle is covered.
- **Tap / Hold / Break**
  - Notes fall in the lane with the same number. Notes in the center C area fall between lanes 4 and 5, and either lane can hit them.
  - Each lane's judge range is the whole lane plus half a lane on each side. One press hits one note: the one closest to its judge time.
  - Mine Taps and mine stars only count as hit by a new press in their own lane near the judge line: sweeps, the neighbouring half lanes and Vertical Judge don't apply. A mine star is judged by its head only; its arrows are never judged.
- **Slides (stars)**
  - The original slide arrows are laid out along the path of the judge areas and fall with the notes (Slide Style "Falling", the default).
  - Judging works like a Drag: each arrow is a check point that is hit when a finger's horizontal position is within two and a half lanes of it on either side (five lanes in total), from 0.3 s before to 0.3 s after it reaches the judge line. The jump between 8 and 1 (the two ends of the lanes) is judged more leniently.
  - Two grades are worked out and the better one counts. By position: every point hit is Critical Perfect, first and last hit is Perfect, first hit plus any other is Great, any hit is Good, nothing hit is Miss. By share: with x points (first and last included) and y hits, y ≥ max(1, ⌊x⌋) is Critical Perfect, y ≥ max(1, ⌊0.75x⌋) Perfect, y ≥ max(1, ⌊0.5x⌋) Great, y ≥ max(1, ⌊0.25x⌋) Good, otherwise Miss. Connected slides are graded as one group.
  - As in standard play, a slide's Perfect is shown as Critical Perfect unless **Detailed Slide Judgment** is on in Mods, so hitting only the first and last points shows Critical Perfect with it off and Perfect with it on.
  - Slide Style "Floating" adds a lighter second judge line 2.4 units above the judge line. Slide arrows arrive there, and only fingers above it play slides (with Vertical Judge off, only up to 2.4 units above it), so the fingers tapping below don't hit slides. Star heads still fall to the judge line.
- **Floating Touch**
  - Touch notes hover above their lane, from low to high in the order A, D, E, B, C.
  - The hit range extends one extra lane on each side. A finger entering or resting in the range counts as a hit. A finger already resting there is judged at the exact judge time.
  - Mine Touches use the original judging: only pressing or sliding into the Touch itself (its own lane, about the size of the graphic) during the 150 ms before it sets it off. A finger already resting there doesn't.
- **Scores**: Falling mode scores are saved locally, separately from normal scores, and are not uploaded. When falling mode is the only mod on, the result screen saves the score as usual. Options that change the chart (Touch to Tap, Slide Tail to Flick set to Start or End, Slide Style set to Standard) count as mods in falling mode: scores are not saved while any of them is on. Options that only change the feel (Vertical Judge, Touch Vertical Judge, Sweep Taps, Break to Flick, Slide Style "Floating", and so on) don't affect saving.

#### Falling settings (Settings → Falling)

The falling-mode settings are in their own **Falling** category and are saved with your settings, so nothing resets when the game restarts. Their descriptions no longer start with "Falling only", but they still only apply in falling mode. Settings from older versions carry over automatically.

| Setting | Description |
|---|---|
| Play Style | Standard or Falling. |
| Note Speed | How fast notes fall. The value means the same as Tap speed. |
| Vertical Judge | On: a press at any height in the right lane counts for Tap, Hold, Break and slides. Off: the press has to be near the judge line. |
| Touch Vertical Judge | Off by default. A floating Touch can be hit at any height in its range (its lane plus one on each side). Independent of Vertical Judge; mine Touches are not affected. |
| Sweep Taps | A held finger sliding into a lane also counts as a tap, so you can sweep fast staircases. |
| Protect Slide Fingers | On by default; only while Sweep Taps is on. A finger that hits a check point of a slide is playing that slide: until the slide (the whole group of connected slides) is judged or the finger lifts, the lanes it moves through are not swept, however far it is from the arrows. Other fingers, even near the slide, sweep as usual. Only one finger per lane is protected at a time (the earliest one); a later one in the same lane sweeps there and is protected again once it leaves. With Slide Style "Standard", a finger counts as playing a star while it is within two and a half lanes of the moving star. When off, every finger sweeps. |
| Slide Style | Standard, Falling (default) or Floating. Standard: stars play as in standard mode (star heads go back to the outer ring and are judged there, slides float in the middle of the screen, looking and judged as in standard play); scores are not saved. Falling: arrows fall along the lanes, see above. Floating: like Falling, with arrows arriving at a second judge line, see above. Settings from older versions: Keep Slides Original on becomes Standard, off becomes Falling. |
| Lane Dividers | Show the vertical lines between lanes. |
| Light Up Touched Lanes | Off by default; only works while Lane Dividers are shown. The lane under your finger lights up and follows your finger, whether or not Sweep Taps is on. |
| Follow Info Display | Off: use the HUD layout above. On: combo and other info follow the display settings. |
| Difficulty Color / Score Color | Color the difficulty (bottom right) and the score (top right) by grade. |
| Judge Line Color | Standard (white); Follow Achievement (gold at ≥100.5%, blue at ≥100%, otherwise white); or Follow Judgment (gold for AP, purple for Great, green for Good, white for Miss). With Slide Style "Floating", the second judge line has the same color, lighter. |

#### Mods in falling mode (Settings → Mod)

Break to Flick, Slide Tail to Flick and Touch to Tap have moved to the **Mod** category, after the other mods. They still only apply in falling mode. Like every mod, they are saved with your settings only while Debug → Save Mod Settings is on; otherwise they reset when the game restarts. Values from older versions carry over.

| Mod | In falling mode |
|---|---|
| Break to Flick | A Break is judged only when you flick on the lane. Doesn't affect saving. |
| Slide Tail to Flick | Off (default), Start or End. Slide arrows are replaced by one red Flick, hit by flicking on its lane, graded by Tap timing (±0.15 s) and counted toward Fast/Late. Start: it reaches the judge line in the lane where the slide starts, at the moment it starts moving. End: it reaches the judge line in the lane where the slide ends, at the moment the star arrives (for connected slides, the end of the last part; for Wifi slides, the end of the middle path). No effect when Slide Style is Standard. Scores are not saved unless it is Off. Older "on" becomes Start. |
| Touch to Tap | Touch and TouchHold notes become Tap and Hold notes falling in their lane. Scores are not saved while this is on. |
| Note Mask | A rectangular mask over the lanes instead of the circle: Outer hides the upper part, so notes appear only shortly before the judge line; Inner hides a band above the judge line, so notes disappear before they arrive. The covered share matches the circular masks. |
| Remove Slides | The falling arrows are not drawn; slides are still judged. With Slide Tail to Flick, the Flick is still shown. |
| Auto Play | Also hits the Flick of Slide Tail to Flick, and lanes light up where Auto Play presses. DJ Auto plays like normal Auto Play. |
| All Touch | Works; together with Touch to Tap, every note becomes a Tap again. |
| Slides With No Head | No effect in falling mode. |
| Playback speed, judge style, Detailed Slide Judgment, All Break, All EX, buttons for Touch | Work as in standard play. The progress bar follows the actual playback speed. |

### Landscape mode

Settings → Display Settings → Screen Orientation, choose "Landscape". Every page is shown in landscape: the main screen is centered, the sub screen is shrunk into the top left corner, and the rest is covered.

### Scores

Scores are shared from the share button on the touch song list (see below). Settings → Backup holds the backups:

| Setting | Description |
|---|---|
| Export Backup | Exports every standard and falling score to the `ScoreBackups` folder (Files app on iOS, `Documents/MajdataPlay/ScoreBackups` on Android). Export Backup and Import Backup show "Done" or "Failed" for a few seconds, then return to "Press +". |
| Backup File / Import Backup | Pick a file in `ScoreBackups` and import it. Scores are merged: for each chart the better achievement, DX score and FC/AP are kept. Older score JSON files are accepted too. |

The share dialog has these options, saved with your settings (also when you cancel):

| Option | Description |
|---|---|
| Share Range | Best, All, or Selected (the charts you picked). Each difficulty of a song is its own entry. A single chart is shared from its detail page. |
| Share Count | How many to share for Best (5–200, default 50); hold "−" / "+" to change it quickly. |
| Share Sort Order | How Best and All are sorted: difficulty level (e.g. 14+ > 14 > 13+; same level: Re:MASTER > MASTER > …), achievement and DX score, compared in any of the 6 possible orders, all from high to low (default: level → achievement → DX). Best is sorted first, then the top ones are taken. |
| Selected Charts Order | Order of the selected charts: the order you picked them in (default), or the Share Sort Order. |
| Share Format | Image, Text, Spreadsheet (CSV) or Data (JSON). The image shows your avatar and nickname at the top left (from the logged-in account, otherwise "Guest") and "Best X" or "All N" with the date at the top right, then score cards with cover art, achievement, grade, FC/AP, DX score and play count ("3 PC"), up to 50 per image. Text, CSV and JSON carry the same header (nickname, Best X / All N, date) and DX score and play count for every entry; files are named like `Nickname-Best50-2026-11-01.csv`. |
| Score Set | Follow Play Style (default), Standard or Falling. |

### Other

- **Touch interface**: Settings → Display Settings → Interface Style, choose "Touch" (default is "Standard", the original song select with its cover carousel). A touch-friendly song list:
  - Categories take the left quarter of the screen; on the right, one song card per row with the cover, title (scrolls when too long), artist and charter, length and BPM (plus plays, likes and comments for online charts; length and BPM show for online charts already downloaded, from playing or opening their details, because each download counts as a play on the server; a song without the global difficulty uses its highest one), difficulty tags with full names and each difficulty's achievement underneath, a heart to favorite, and the best score with a colored grade, FC/AP, DX score and "X PC · last played".
  - The top bar reads "Cover  −  difficulty  +  Settings" on the left, with your nickname and avatar on the right ("Guest" when not logged in). "−" / "+" switch the global difficulty without losing your scroll position; a draggable scrollbar on the right. Difficulty and pages wrap around at either end.
  - At the bottom, a floating pager sits between a magnifier and a share button. Tap the magnifier to expand a search bar (the keyboard opens); search with the keyboard's return key or by tapping the magnifier again, which also folds the bar back; "×" clears the search. While a search filters the list, the folded magnifier turns blue. While the on-screen keyboard is up, the whole floating row moves above it (or to the top of the list when there is no room), and the system input bar is hidden.
  - The share button opens the share dialog (options above), with "Cancel" and "Share". With Share Range set to Selected, the dialog reads "Tap the difficulties to share (N selected)" with a button on the right, "Start Picking" while nothing is picked and "Edit Picks" afterwards: it enters selection mode, where tapping difficulty tags picks or unpicks those charts (every tag shows "+" when not picked and "−" with the difficulty color when picked). Picks are kept across pages and categories and saved with your settings. A floating bar shows "N selected · Clear · Done"; "Done" keeps your picks, leaves selection mode and brings the dialog back. Outside selection mode the tags look as usual.
  - The detail page shows the same "Cover  −  difficulty  +  Settings" controls next to "Back" and your nickname and avatar at the top right; there "−" / "+" only cycle through the difficulties this song has, without changing the global difficulty. Opening settings from the detail page brings you back to it. Songs per page is a setting (Display Settings → Songs per List Page, default 20).
  - Tap a card for chart details: big cover with a heart, share/export buttons and an info card on the left; title, difficulty bar (each difficulty's achievement underneath, as on the cards), score card (achievement, grade, FC/AP, then DX score with stars and a gold progress bar like the result screen, then plays and the last play date), chart radar and note density on the right; "Back" at the top left and two large floating icon buttons (160 units, smaller on short screens) at the bottom right: ▶ (Play) and, left of it for charts from majdata.net while you are logged in, a speech bubble (Feedback). Feedback opens a dialog that loads your current vote, where you can like or dislike (tap again to take the vote back) and post a comment; "Submit" sends it, then the like and comment counts are refreshed. Opening and closing details slides and fades, and the preview and background music fade in and out.
  - Pressing Play shows a new loading screen (cover, difficulty, title, artist and charter, best score and loading progress) that fades out as the game opens. After playing a chart started from its detail page, you come back to that detail page, with the list where you left it.
  - The list fades out and in together with the game's scene transition when entering settings or coming back.
  - The category sidebar starts with a title bar (app icon, "MajdataPlay", "urntt edition"); tap the icon to open this repository on GitHub. All cover art has rounded, outlined corners, including the share image.
- **Re:MASTER color**: Re:MASTER is now pink (`#EE5DB0`) on the touch screens and the share image, so it stands apart from MASTER. UTAGE keeps its color.
- **Phones**: the touch screens (song list, details, settings, result and loading screen) are scaled by the physical screen size: the short side of the screen holds at most 250 units per inch (at least 600 units), so text and buttons on phones are about 1.7 times larger than before, while tablets and computers stay as they were. Content stays inside the safe area while backgrounds fill the screen.
  - **Narrow screens** (phones in portrait): the category sidebar of the song list and the settings becomes a drawer opened by a "Categories" button at the top left and closed after picking; only the avatar is shown at the top right; song cards have two rows (small cover, title, credits, info and best score, then the difficulty tags across the card); the selection bar stacks above the pager and the search bar covers the floating row while open; the detail page is a single column (cover, share and export, title and difficulties, score card, info card, radar, note density) with the top controls on a second row; option values in settings get narrower; dialogs shrink to fit.
  - **Short screens** (phones in landscape): the sidebar is 25% of the width, between 260 and 360 units; song cards are lower; covers on the detail and result pages are limited by the screen height; the large icon buttons are at most 16% of the screen height.
- **New settings and result screens** (only when Interface Style is "Touch"):
  - **Settings**: categories on the left (chart settings first, with the current song's title), one card per option on the right with its name, the full description and the value between "−" and "+" (hold to repeat). "Calibrate" and "Done" at the top right. Cabinet buttons still work: A2/A7 switch categories, A3/A6 select options, the touch sensors change values, A4/A5 leave and A1 opens calibration. Tap the "Render Quality" card 10 times (or press A8 10 times on it) to unlock custom rendering.
  - **Result**: your nickname and avatar at the top right; rounded cover with a favorite heart, difficulty, title, artist and charter; an upload button for online charts; the achievement rolls up next to its grade and FC/AP, compared with your previous best; DX score with stars; a judgement table per note type (TAP, HOLD, SLIDE, TOUCH, BREAK and total); FAST/LATE, average offset and the timing distribution. Large icon buttons (160 units, smaller on short screens) at the bottom right, from left to right: Feedback (speech bubble, only for charts you can rate; opens the original feedback panel to like, dislike or comment), Share (shares this play as image, text, CSV or JSON), Play Again (↻) and Continue (→). Continue, Play Again and Feedback stay faded until the score is saved. On the first play of a chart you can rate, Continue opens the feedback panel first, like the original screen. Play Again plays the same chart again with the loading screen of the song list; after that play, Continue still brings you back to the detail page you started from. The difficulty tag is as wide as the cover. "Classic view" switches back to the original result screen for this result. Dan and practice keep the original screen.
  - "Cover" switches back to the carousel for this visit.
- **Instant Song Preview**: Settings → Display Settings, off by default. When on, switching songs on the song list plays the new preview right away instead of returning to the background music in between.
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
