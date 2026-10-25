# 打包未签名 ipa，用 Sideloadly 装到 iPad

下面两种方式打出来的都是**未签名**的 `.ipa`，不需要 Apple 开发者证书。安装时由 Sideloadly 用你自己的 Apple ID 签名。

| | 本地脚本 | GitHub Actions |
|---|---|---|
| 需要什么 | Mac 上装好 Unity 6000.3.17f1（含 iOS Build Support）和 Xcode | 仓库里配置好 Unity 许可证 |
| 每次操作 | 关掉 Unity，执行一条命令 | 网页上点 Run workflow，或提交信息里带 `[ipa]` |
| 速度 | 快，可以增量构建 | 慢，每次几十分钟到一小时 |

## 方式一：Mac 本地脚本

### 一次性准备

1. Unity Hub 里给 6000.3.17f1 加装 **iOS Build Support** 模块。
2. 从 App Store 安装 Xcode，打开一次同意协议，然后在终端执行：
   ```bash
   sudo xcode-select -s /Applications/Xcode.app
   ```
3. 安装 [Sideloadly](https://sideloadly.io/)。

### 打包

关掉打开着本项目的 Unity，在项目根目录执行：

```bash
Tools/iOS/build_unsigned_ipa.sh
```

完成后会打印 ipa 的路径，默认是 `~/MajdataBuilds/iOS/MajdataPlay.ipa`。

- 第一次运行会把工程切到 iOS 平台并重新导入资源，比较慢。
- 之后回到编辑器里调试 macOS 时，需要在 Build Profiles 里切回 macOS 平台。

可以用环境变量调整（写在命令前面）：

| 变量 | 默认值 | 说明 |
|---|---|---|
| `OUTPUT_DIR` | `~/MajdataBuilds/iOS` | 输出目录。不要放在 OneDrive、iCloud 等同步文件夹里，容易编译失败 |
| `BUNDLE_ID` | `com.<Mac 用户名>.majdataplay` | App 包名。官方包名属于上游作者的账号，免费 Apple ID 签不了 |
| `SKIP_UNITY` | `0` | 设为 `1` 时跳过 Unity 导出，直接用上次导出的 Xcode 工程编译 |
| `UNITY` | Unity Hub 默认安装位置 | Unity 可执行文件的路径 |

例如：`BUNDLE_ID=com.example.majdataplay Tools/iOS/build_unsigned_ipa.sh`

失败时请查看输出目录里的 `unity_build.log` 或 `xcodebuild.log`。

## 方式二：GitHub Actions 云端打包

工作流文件是 `.github/workflows/build-ios-unsigned.yml`，在 GitHub 的 macOS 机器上打包。公开仓库免费、不限分钟数。

### 一次性准备

1. **启用 Actions**：fork 的仓库默认禁用工作流。在仓库的 **Actions** 页面点击启用。
   - 上游的 `Build Project`（`main.yml`）需要原作者的苹果证书，在你的仓库里会失败，建议在 Actions 页面左侧选中它，点 `…` → **Disable workflow**。
2. **配置 Secrets**：在仓库 **Settings → Secrets and variables → Actions → New repository secret** 添加三项：
   - `UNITY_EMAIL`：Unity 账号邮箱
   - `UNITY_PASSWORD`：Unity 账号密码
   - `UNITY_LICENSE`：Mac 上 `/Library/Application Support/Unity/Unity_lic.ulf` 文件的全部内容。用 Unity Hub 登录并激活过个人版许可证后，这个文件就会生成。可以这样复制：
     ```bash
     pbcopy < "/Library/Application Support/Unity/Unity_lic.ulf"
     ```
3. **（可选）让网页上出现 Run workflow 按钮**：GitHub 要求手动触发的工作流文件在默认分支（`dev`）上。把 `.github/workflows/build-ios-unsigned.yml` 合进 `dev` 一次即可，之后运行时可以选任意分支。

### 打包

任选一种：

- **网页触发**：Actions → **Build unsigned iOS IPA** → **Run workflow**，选择分支后运行。可以填包名和保留天数。需要先完成上面的第 3 步。
- **提交触发**：在任意分支推送一个提交信息里带 `[ipa]` 的提交，例如：
  ```bash
  git commit --allow-empty -m "build [ipa]" && git push
  ```

运行完成后，在这次运行的页面底部 **Artifacts** 里下载 `MajdataPlay-iOS-<提交号>`。下载的是 zip，解压后就是 ipa。

- 默认保留 7 天。
- 第一次没有缓存，大约一小时；之后会快一些。
- 失败时会额外上传 `build-logs-<提交号>`，里面是编译日志。
- 个人版许可证偶尔会激活失败，重新运行一次一般就好。

## 用 Sideloadly 安装

1. 用数据线连接 iPad，在 iPad 上点“信任此电脑”。
2. 打开 Sideloadly，把 ipa 拖进去，填 Apple ID，点 **Start**。第一次需要输入 Apple ID 密码；开了双重认证的还要输入验证码。
3. 第一次安装后还需要在 iPad 上操作：
   - **开发者模式**（iPadOS 16 及以上）：设置 → 隐私与安全性 → 开发者模式 → 开启，iPad 会重启。
   - **信任开发者**：设置 → 通用 → VPN 与设备管理 → 你的 Apple ID → 信任。
4. 免费 Apple ID 签名的 App **7 天后失效**，到期前用 Sideloadly 再装一次即可，数据保留。Sideloadly 支持自动续签：Mac 和 iPad 在同一个 Wi‑Fi 下，开启 Sideloadly 的自动续签即可。
5. 更新时直接用新的 ipa 覆盖安装（包名保持不变），数据保留。

谱面放在“文件”App → 我的 iPad → MajdataPlay → MaiCharts。也可以对 .zip / .adx 选“共享 → 用 MajdataPlay 打开”导入。
