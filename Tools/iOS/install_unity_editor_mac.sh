#!/usr/bin/env bash
# Installs the Unity Editor with the iOS module where game-ci/unity-builder expects it on macOS:
#   /Applications/Unity/Hub/Editor/<version>
# game-ci skips its own installation when the editor is already there. Its own installation runs
# "Unity Hub -- --headless install", which newer Unity Hub releases refuse ("'install' is not supported by the
# native Unity Hub"), so the editor is installed here first:
#   1. with the standalone Unity CLI (https://unity.com/install.sh, then "unity install");
#   2. if that fails, from the editor and iOS support installer packages of the release.
#
# The version and changeset are read from ProjectSettings/ProjectVersion.txt; UNITY_VERSION and UNITY_CHANGESET
# override them. Run from the repository root on a macOS runner (uses sudo).
set -euo pipefail

project_version_file="ProjectSettings/ProjectVersion.txt"
version="${UNITY_VERSION:-$(awk '/^m_EditorVersion:/ {print $2}' "$project_version_file")}"
changeset="${UNITY_CHANGESET:-$(sed -n 's/^m_EditorVersionWithRevision: .*(\([0-9a-f]*\)).*/\1/p' "$project_version_file")}"
if [ -z "$version" ] || [ -z "$changeset" ]; then
  echo "::error::Could not read the Unity version and changeset from $project_version_file"
  exit 1
fi

hub_editor_root="/Applications/Unity/Hub/Editor"
target_dir="$hub_editor_root/$version"
cli_timeout_sec=2700

# Checks that a directory holds the editor and its iOS module.
editor_ready() {
  local dir="$1"
  if [ ! -x "$dir/Unity.app/Contents/MacOS/Unity" ]; then
    return 1
  fi
  [ -d "$dir/PlaybackEngines/iOSSupport" ] || [ -d "$dir/Unity.app/Contents/PlaybackEngines/iOSSupport" ]
}

# Prints the directory that holds Unity.app of this version, or nothing when it is not found.
find_editor_dir() {
  local app
  app="$(find /Applications "$HOME" -maxdepth 6 -type d -name Unity.app -path "*$version*" -prune 2>/dev/null | head -n 1 || true)"
  if [ -n "$app" ]; then
    dirname "$app"
  fi
}

# Runs a command and stops it after a number of seconds; stdin is closed so a prompt cannot wait forever.
run_with_timeout() {
  local seconds="$1"
  shift
  perl -e 'alarm shift; exec @ARGV' "$seconds" "$@" </dev/null
}

install_with_cli() {
  echo "Installing Unity $version ($changeset) with the Unity CLI"
  if ! curl -fsSL https://unity.com/install.sh | CI=1 UNITY_CLI_SKIP_SETUP=1 bash; then
    echo "::warning::Could not install the Unity CLI"
    return 1
  fi
  local cli="$HOME/.unity/bin/unity"
  if [ ! -x "$cli" ]; then
    echo "::warning::The Unity CLI is not at $cli"
    return 1
  fi
  "$cli" --version || true
  if ! run_with_timeout "$cli_timeout_sec" "$cli" install "$version" -c "$changeset" -m ios -a arm64 --cm; then
    echo "::warning::The Unity CLI could not install Unity $version"
    return 1
  fi
}

install_with_packages() {
  local base="https://download.unity3d.com/download_unity/$changeset"
  local work
  work="$(mktemp -d)"
  echo "Installing Unity $version ($changeset) from the installer packages"
  curl -fL --retry 3 -o "$work/editor.pkg" "$base/MacEditorInstallerArm64/Unity-$version.pkg"
  curl -fL --retry 3 -o "$work/ios.pkg" "$base/MacEditorTargetInstaller/UnitySetup-iOS-Support-for-Editor-$version.pkg"
  sudo installer -pkg "$work/editor.pkg" -target /
  sudo installer -pkg "$work/ios.pkg" -target /
  # The packages install into /Applications/Unity; move them into the Hub layout game-ci looks for.
  sudo mkdir -p "$target_dir"
  sudo mv /Applications/Unity/Unity.app "$target_dir/"
  if [ -d /Applications/Unity/PlaybackEngines ]; then
    sudo mv /Applications/Unity/PlaybackEngines "$target_dir/"
  fi
  rm -rf "$work"
}

if editor_ready "$target_dir"; then
  echo "Unity $version is already installed at $target_dir"
  exit 0
fi

if install_with_cli; then
  editor_dir="$(find_editor_dir)"
  echo "The Unity CLI installed Unity $version at: ${editor_dir:-<not found>}"
  if [ -n "$editor_dir" ] && [ "$editor_dir" != "$target_dir" ]; then
    sudo mkdir -p "$hub_editor_root"
    sudo rm -rf "$target_dir"
    sudo ln -s "$editor_dir" "$target_dir"
  fi
fi

if ! editor_ready "$target_dir"; then
  echo "::warning::No usable Unity $version with iOS support at $target_dir after the Unity CLI; using the installer packages"
  sudo rm -rf "$target_dir"
  install_with_packages
fi

if ! editor_ready "$target_dir"; then
  echo "::error::Unity $version with iOS support is not at $target_dir"
  ls -la "$target_dir" || true
  exit 1
fi
echo "Unity $version is ready at $target_dir"
