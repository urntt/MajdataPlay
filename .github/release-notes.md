基于 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的 Fork，新增**下落式玩法**（音符沿 8 条轨道落到判定线）和**横屏模式**。新增内容的完整说明见仓库首页 README。

## 本版更新

以下功能都在触摸式界面里（设置 → 通用设定 → 界面样式选“触摸”）。

**谱面导入**
- 选歌侧栏底部新增“导入谱面”。
- iOS 和 Android 上会打开系统的文件选择器，可以选 `.zip`、`.adx`（AstroDX）和 `.pez`（Phira）。
- 电脑上会列出游戏目录下 `ChartImports` 文件夹里的文件，点一下就导入。
- iOS 也可以在“文件”里对这三种文件选“共享 → 用 MajdataPlay 打开”。
- `.pez` 会用 [Universal Rhythm Chart](https://github.com/urntt/universal-rhythm-chart) 转换成 simai，放进“Import”分类。

**谱面导出**
- 本地谱面的详情页里，“游玩”左边新增“导出”按钮，可选三种格式：
  - ZIP：整个谱面文件夹；
  - ADX：给 AstroDX 用；
  - PEZ：当前选中的难度，转换成 Phira 谱面。大谱面需要几十秒。
- 导出后会打开系统分享面板，可以存到“文件”，或者发给其他 App。
- 在线谱面暂时不能导出。

**刷新谱面**
- 选歌侧栏底部新增“刷新谱面”：重新扫描本地和在线谱面，然后回到原来的分类和页码。

**分享成绩时全选分类**
- 选择要分享的谱面时，底部浮窗新增“全选”：一键选上当前分类里所有玩过的难度（有搜索时只算搜索结果）。
- 按分享设置里的“成绩来源”判断玩没玩过。
- 全都选上后按钮变成“取消全选”，只取消这个分类里的；分类里一首都没玩过时按钮变灰。

**触摸式评价窗口**
- 结算界面的“评价”按钮修好了：以前一直不出现，现在会在服务器确认可以评价后出现。
- 点“评价”会打开和选歌详情页一样的触摸式窗口，不再切回原版界面。
- 第一次打一张可以评价的谱面时，点“继续”会先弹出这个窗口，有“跳过”和“提交”两个按钮，点哪个都会离开结算。
- 写评论时，窗口会移到键盘上方。
- 评价按钮的显示条件改成和上游相同：在线谱面、已登录、服务器需要登录。以前只认 majdata.net。

这些改动做了代码检查，`.pez` 转换也和 URC 命令行逐字节比对过，但没有在 Unity 或真机上运行过。请重点看看：
- iOS 上的文件选择器和分享面板能不能正常弹出和返回；
- `.pez` 在 iOS 上能不能正常导入、导出；
- 导出的 `.adx` 和 `.pez` 能不能在 AstroDX 和 Phira 里打开；
- 评价窗口的样子，以及键盘弹出时的位置。
- 选择浮窗加了“全选”后变宽了，手机上的排版是否正常。

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
