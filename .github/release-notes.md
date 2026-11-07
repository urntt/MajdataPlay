基于 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的 Fork，新增**下落式玩法**（音符沿 8 条轨道落到判定线）和**横屏模式**。新增内容的完整说明见仓库首页 README。

## 本版更新

修正上一版（#30）在 iPad 和手机上发现的问题：

- **Slide样式**：
  - 更新后统一为“下落式”，只重置这一次；之后自己改的会保留。
  - 旧版“Slide保持原样”的开关不再继承。
- **手机横屏下落式**：
  - 曲名和难度放在判定线下方，空间小时自动缩小字号，不再压在判定线上。
  - 暂停键和分数离屏幕边的距离，取原边距和刘海安全区中较大的一个，不再两者相加，所以不会再缩到很里面。
- **iPad 下落式**：底部曲名和难度回到原来的位置（不再为底部横条往上让）。四角位置和以前一样。
- **星星音效**：手指提前停在星星经过的位置时，音效等命中的检查点到达判定线才播放，不再提前。
- **悬空式**：星星的判定文字显示在第二判定线上方。
- **详情页、结算页右下角按钮**：
  - 缩小到原来的 75%；
  - 按钮之间的间距加大到按钮边长的四分之一；
  - 离屏幕下边和右边的距离相等。

以上改动只做了代码检查和逻辑测试，没有在 Unity 或真机上运行过，请在 iPad 和手机上看看。

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
