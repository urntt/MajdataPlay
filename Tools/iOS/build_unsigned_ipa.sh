#!/usr/bin/env bash
# 打包未签名的 iOS ipa，之后用 Sideloadly 等工具以自己的 Apple ID 签名安装。
# 用法：关掉打开着本项目的 Unity，在 Mac 终端执行  Tools/iOS/build_unsigned_ipa.sh
# 详细说明见同目录的 README.md。
#
# 可用环境变量：
#   OUTPUT_DIR         输出目录，默认 ~/MajdataBuilds/iOS（不要放在 OneDrive/iCloud 等同步文件夹里）
#   UNITY              Unity 可执行文件，默认按 ProjectSettings/ProjectVersion.txt 的版本在 Unity Hub 默认位置查找
#   SKIP_UNITY=1       跳过 Unity 导出，直接用已有的 Xcode 工程编译
#   XCODE_PROJECT_DIR  Unity-iPhone.xcodeproj 所在目录，默认 $OUTPUT_DIR/xcode
#   BUNDLE_ID          ipa 里 App 的包名，默认 com.<Mac 用户名>.majdataplay
#   IPA_NAME           输出文件名，默认 MajdataPlay.ipa
set -euo pipefail

log() { printf '\n==> %s\n' "$*"; }
fail() { printf '\n错误：%s\n' "$*" >&2; exit 1; }

[[ "$(uname -s)" == "Darwin" ]] || fail "只能在 macOS 上运行。"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTPUT_DIR="${OUTPUT_DIR:-$HOME/MajdataBuilds/iOS}"
XCODE_PROJECT_DIR="${XCODE_PROJECT_DIR:-$OUTPUT_DIR/xcode}"
IPA_NAME="${IPA_NAME:-MajdataPlay.ipa}"
DEFAULT_BUNDLE_OWNER="$(id -un | tr '[:upper:]' '[:lower:]' | tr -cd 'a-z0-9-')"
BUNDLE_ID="${BUNDLE_ID:-com.${DEFAULT_BUNDLE_OWNER:-user}.majdataplay}"
SKIP_UNITY="${SKIP_UNITY:-0}"

mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"
case "$OUTPUT_DIR" in
    *CloudStorage*|*OneDrive*|*"Mobile Documents"*)
        echo "警告：输出目录在云同步文件夹里（$OUTPUT_DIR），Xcode 编译和签名容易出错，建议换到本地目录。" >&2
        ;;
esac

command -v xcodebuild >/dev/null 2>&1 || fail "找不到 xcodebuild。请从 App Store 安装 Xcode，打开一次同意协议，再执行 sudo xcode-select -s /Applications/Xcode.app"

# 1. Unity 导出 Xcode 工程
if [[ "$SKIP_UNITY" != "1" ]]; then
    UNITY_VERSION="$(awk '/^m_EditorVersion:/ {print $2}' "$ROOT/ProjectSettings/ProjectVersion.txt")"
    UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
    [[ -x "$UNITY" ]] || fail "找不到 Unity $UNITY_VERSION（$UNITY）。请用 Unity Hub 安装该版本并勾选 iOS Build Support，或用 UNITY=... 指定路径。"
    if pgrep -lf "Unity.app/Contents/MacOS/Unity" 2>/dev/null | grep -F -- "$ROOT" >/dev/null; then
        fail "Unity 正打开着这个项目，请先关闭 Unity 再运行。"
    fi

    UNITY_LOG="$OUTPUT_DIR/unity_build.log"
    log "Unity 导出 Xcode 工程（第一次会切换到 iOS 平台并重新导入资源，比较慢）"
    echo "日志：$UNITY_LOG"
    rm -rf "$XCODE_PROJECT_DIR"
    if ! "$UNITY" -batchmode -quit \
            -projectPath "$ROOT" \
            -buildTarget iOS \
            -executeMethod MajdataPlay.Editor.CustomBuild.BuildRelease \
            -customBuildPath "$XCODE_PROJECT_DIR" \
            -customBuildName MajdataPlay \
            -logFile "$UNITY_LOG"; then
        tail -n 60 "$UNITY_LOG" >&2 || true
        fail "Unity 导出失败，完整日志见 $UNITY_LOG"
    fi
fi

PROJECT="$XCODE_PROJECT_DIR/Unity-iPhone.xcodeproj"
[[ -d "$PROJECT" ]] || fail "找不到 Xcode 工程 $PROJECT"

# 2. 不签名编译
ARCHIVE="$OUTPUT_DIR/MajdataPlay.xcarchive"
XCODE_LOG="$OUTPUT_DIR/xcodebuild.log"
log "xcodebuild 编译（不签名）"
echo "日志：$XCODE_LOG"
rm -rf "$ARCHIVE"
if ! xcodebuild \
        -project "$PROJECT" \
        -scheme Unity-iPhone \
        -configuration Release \
        -sdk iphoneos \
        -destination 'generic/platform=iOS' \
        -archivePath "$ARCHIVE" \
        archive \
        CODE_SIGNING_ALLOWED=NO \
        CODE_SIGNING_REQUIRED=NO \
        CODE_SIGN_IDENTITY="" \
        DEVELOPMENT_TEAM="" \
        > "$XCODE_LOG" 2>&1; then
    grep -E "error:|\*\* ARCHIVE FAILED" "$XCODE_LOG" | tail -n 40 >&2 || tail -n 40 "$XCODE_LOG" >&2
    fail "xcodebuild 编译失败，完整日志见 $XCODE_LOG"
fi

APP="$(find "$ARCHIVE/Products/Applications" -maxdepth 1 -name '*.app' -type d | head -n 1)"
[[ -n "$APP" ]] || fail "编译结果里找不到 .app"

# 3. 打包 ipa
log "打包 ipa"
PAYLOAD_ROOT="$OUTPUT_DIR/ipa"
rm -rf "$PAYLOAD_ROOT"
mkdir -p "$PAYLOAD_ROOT/Payload"
cp -R "$APP" "$PAYLOAD_ROOT/Payload/"
APP_IN_PAYLOAD="$PAYLOAD_ROOT/Payload/$(basename "$APP")"
# 只改 App 的包名，不动内嵌的 UnityFramework，避免两者包名重复
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $BUNDLE_ID" "$APP_IN_PAYLOAD/Info.plist"
xattr -cr "$PAYLOAD_ROOT/Payload"
IPA="$OUTPUT_DIR/$IPA_NAME"
rm -f "$IPA"
(cd "$PAYLOAD_ROOT" && zip -qry "$IPA" Payload)
rm -rf "$PAYLOAD_ROOT"

log "完成：$IPA"
echo "包名：$BUNDLE_ID"
echo "用 Sideloadly 打开这个 ipa，登录 Apple ID 后安装到 iPad。"
