基于 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的 Fork，新增 Phigros 式的**下落式玩法**和**横屏模式**。新增内容的完整说明见仓库首页 README。

## 本版更新

- **分享小窗**：范围选“自选”时：
  - 左边显示“点击想分享的谱面难度（已选 N 个）”；
  - 右边按钮在没选时是“开始选择”，选了以后是“编辑选择”。
- **谱面详情页**：右下角改为图标键。
  - ▶：游玩。
  - 对话气泡：评价，在 ▶ 左边。majdata.net 的谱面、已登录时显示。
    - 可以点赞、点踩（再点一次撤销）、发评论；
    - 提交后点赞数和评论数会刷新。
- **结算页**：右下角从左到右是四个图标键：
  - 评价（仅可评价的谱面）；
  - 分享；
  - 再玩一次；
  - 继续。

  保存完成之前，评价、再玩一次、继续是淡的、不能点。
- **设置**：界面样式、选歌列表每页数量、选歌即时切换预览移到“显示设定”，放在画面方向下面。原来设定的值升级后保持不变。

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

设置 → 下落式 → 游玩模式，选“下落式”。下落式的全部设置都在“下落式”分类里，随设置保存；横屏在“显示设定 → 画面方向”。
