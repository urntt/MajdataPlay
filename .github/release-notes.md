基于 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的 Fork，新增**下落式玩法**（音符沿 8 条轨道落到判定线）和**横屏模式**。新增内容的完整说明见仓库首页 README。

## 本版更新

这一版重做了谱面导入，解决导入失败、导入后不显示封面和背景的问题。

**按内容识别，不看后缀**
- 任何 zip 都能导入：`.zip`、`.adx`、`.pez`，后缀写错的也行。压缩包里再套压缩包（比如一包 .adx）也会逐个导入，最多三层。
- 谱面文件夹可以放在任意深度，不再只认一两层。mac 压缩时带的 `__MACOSX` 会被忽略。
- iOS 的“导入谱面”可以选任何文件，也可以直接选文件夹；Android 点“导入谱面”后可以选“选择文件”或“选择文件夹”；电脑上 `ChartImports` 文件夹里的文件和子文件夹都会列出来。

**导入后封面、背景能显示了**
- 游戏只认 `track.*`、`bg.png`/`bg.jpg`、`pv.*` 这几个名字。导入时会按内容自动改名，例如 `song.ogg` → `track.ogg`，`cover.jpg`、`Jacket.PNG` → `bg.jpg`/`bg.png`，`movie.mp4` → `pv.mp4`，`maidata.txt.txt` → `maidata.txt`。
- Windows 上用 GBK 或 Shift-JIS 存的中文、日文文件名不再乱码，不同文件夹也不会再被合并到一起。
- 用 GBK、Shift-JIS 或 UTF-16 保存的 maidata.txt 会转成 UTF-8，曲名不再乱码。

**导入失败会说明原因**
- 加载画面会显示每一步在做什么，最后显示“成功几个、失败几个”和失败原因。
- 回到触摸式选歌界面后，会弹出“导入结果”窗口，列出每个失败的原因和改过的文件名。
- 常见原因有：RAR/7z 压缩包（请解压或改成 zip）、只选了 maidata.txt 没有音乐、找不到音乐、音乐是 M4A/FLAC（游戏不支持）、官方 Phigros 格式的谱面（暂不支持）。

**内置的 URC 更新到最新**
- 能导入 PhiEdit（PEC）格式的 Phira 谱面，包括名字叫 `.json` 的 PEC 谱面（以前会直接失败）。
- 按 Phira 的规则读 `info.yml`、旧的 `info.txt` 和 `offset`。
- 转成 simai 时，第一小节的写法和 URC 命令行保持一致。

这些改动在电脑上用真实的压缩包和文件夹测过识别和改名（GBK、Shift-JIS、嵌套、错误后缀等），`.pez` 转换也和 URC 命令行逐字节比对过，但没有在 Unity 或真机上运行过。请重点看看：
- iOS 选择器能不能选文件夹、选任意文件；
- Android 的“选择文件夹”；
- 你之前导入失败、或导入后没有封面的谱面，现在是否正常；
- “导入结果”窗口的样子。

## 下载

| 文件 | 适用 |
|---|---|
| `MajdataPlay-*-unsigned.ipa` | iPhone / iPad，未签名，用 Sideloadly 等工具以自己的 Apple ID 签名安装 |
| `MajdataPlay-*.apk` | Android，直接安装 |

## 安装

### iOS / iPadOS
1. 安装 [Sideloadly](https://sideloadly.io/)，用数据线连接设备。
2. 把 ipa 拖进 Sideloadly，填写 Apple ID 后点 Start。
3. 第一次安装后：设置 → 隐私与安全性 → 开启开发者模式；设置 → 通用 → VPN 与设备管理 → 信任你的 Apple ID。
4. 免费 Apple ID 签名的 App 7 天后失效，到期前重新安装一次即可，数据保留。

详细步骤见 [Tools/iOS/README.md](https://github.com/urntt/MajdataPlay/blob/feature/landscape-falling-mode/Tools/iOS/README.md)。

### Android
- 包名为 `com.urntt.majdataplay`，可与官方版同时安装。
- 谱面目录与官方版相同：`/sdcard/Documents/MajdataPlay`（第一次启动时需允许存储权限）。
- 如果这次的签名与之前装的不同，覆盖安装会失败，先卸载旧版再安装。

## 怎么开下落式

设置 → 下落式 → 游玩模式，选“下落式”。下落式的设置在“下落式”分类里，随设置保存；Break转Flick、星星尾转Flick、Touch转Tap 在“Mod”分类里。横屏在“渲染设定 → 画面方向”。
