基于 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的 Fork，新增**下落式玩法**（音符沿 8 条轨道落到判定线）和**横屏模式**。新增内容的完整说明见仓库首页 README。

## 本版更新

- **手机横屏下落式**：四角的暂停键、分数、达成率、曲名和难度不再放大，和 iPad 同比例（比上一版小约 35%）；暂停菜单仍按手机放大。
- **扫键时保护星星手指**：
  - 只保护按下时就在星星头或星星当前位置附近（左右各一列）的手指；中途从别处划过来的手指照常扫键。
  - 每条星星同时只保护一根：最先打到它的那根。
  - 这根抬起后，接着打这条星星的另一根（同样须是按下时就在星星上的手指）继续受保护。

以上改动只做了代码检查和逻辑测试，没有在 Unity 或真机上运行过，请在手机上看看。

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

设置 → 下落式 → 游玩模式，选“下落式”。下落式的设置在“下落式”分类里，随设置保存；Break转Flick、星星尾转Flick、Touch转Tap 在“Mod”分类里。横屏在“显示设定 → 画面方向”。
