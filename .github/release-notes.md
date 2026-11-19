基于 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的 Fork，新增**下落式玩法**（音符沿 8 条轨道落到判定线）和**横屏模式**。新增内容的完整说明见仓库首页 README。

## 本版更新

合并了上游 MajdataPlay 到 2.1.2（截至 2026-11-19 的 77 个提交）。

**上游带来的主要变化**：
- 设置拆成两份：用户设置（`settings.json`，登录后随账号同步）和系统设置（`system-settings.json`，只存本机），部分选项换了分类。
- 谱面解析换成 MajSimai 3.0。
- 成绩改为按每个难度的内容 Hash 保存，并支持新的在线成绩接口。
- 在线设置同步、新的难度选择、界面毛玻璃样式、Android 刷新率等。

**本 fork 随之做的调整**：
- 下落式、备份、保存的 Mod 设置，以及界面样式、选歌列表每页数量、选歌即时切换预览、画面方向，都只存在本机的系统设置里，不随账号同步。用官方版登录同一账号也不会把它们冲掉。
- 旧版设置会自动搬过去，不用重新设置。
- 设置界面里的位置变了：
  - 界面样式、选歌列表每页数量、选歌即时切换预览在“通用设定”；
  - 画面方向在“渲染设定”。
- 旧的下落式成绩会在选歌列表加载后迁移到新的存储方式。
- 备份文件改为按难度 Hash 保存；旧备份仍可导入，但只导入当前歌曲列表里有的谱面。
- 选歌列表和反馈面板改用上游新的点赞、评论接口。

以上改动做了代码检查和设置迁移测试，没有在 Unity 或真机上运行过。请重点看看：
- 设置是否都还在；
- 下落式成绩是否还在；
- 上游的新界面和我们的列表、结算界面放在一起是否正常。

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
