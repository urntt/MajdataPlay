基于 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的 Fork，新增 Phigros 式的**下落式玩法**和**横屏模式**。新增内容的完整说明见仓库首页 README。

## 本版更新

- **合并上游 2.1.0**：
  - 收藏等运行数据改存数据库；
  - 联网谱面缓存去重；
  - Vulkan 低延迟垂直同步；
  - 连接 Slide 的新箭头样式等。
- **Phigros 式界面**：设置 → 游戏设置 → 界面样式（原“选歌样式”），选“Phigros式”。
  - 顶栏控件靠左，右侧显示玩家昵称和头像。
  - 翻页浮窗左边的放大镜点开搜索；右边的分享键打开分享小窗。
  - 分享小窗可选：
    - 范围（最好的若干个 / 全部 / 自选）；
    - 数量、排序、格式和成绩分区。
  - 范围为“自选”时，点“选择谱面（点击难度）”勾选要分享的谱面。
  - 详情页去掉“加入自选”键。
- **结算页**：
  - 右上角显示玩家信息；
  - “分享成绩”和“评价”在同一行；
  - 新增“再玩一次”。
- **下落式**：暂停键、曲名、分数、难度不再发暗。
- **设置**：分享相关的设置移到分享小窗；“成绩”类别改名为“备份”，只留导出和导入备份。

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
