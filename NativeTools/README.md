# NativeTools

以下のフォルダ構成で ADB バイナリを配置してください。

```text
AdwScanGui/NativeTools/
  win_x64/
    adb.exe         ← Windows版
    AdbWinApi.dll   ← 必要に応じて
  osx_x64/
    adb             ← macOS Intel版
  osx_arm64/
    adb             ← macOS Apple Silicon版
  linux_x64/
    adb             ← Linux版
```

**ダウンロード先**: [Android SDK Platform-Tools](https://developer.android.com/tools/releases/platform-tools)

バイナリが存在しない場合、アプリは自動的に PATH 上の `adb` を使用します。
