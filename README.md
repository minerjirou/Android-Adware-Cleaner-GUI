# Android-Adware-Cleaner-GUI (AdwScan)

怪しいクリーナー系アプリを入れたらトップ画面が変になってしまったときに使えると思うGUIツールです。
Android 端末内に潜む意図しないアドウェアや不審なアプリを検知し、安全に無効化・削除・復元するための機能を提供します。

## 機能概要

- **📡 モニター**: リアルタイムで端末の Logcat アクティビティを監視します。
- **📋 アプリ一覧**: インストールされているユーザーアプリの一覧と状態を表示します。
- **🔍 詳細検査**: パッケージのアクティビティ履歴から脅威スコアを算出し、不審な挙動の理由を特定します。
- **⚡ 実行アクション**: アプリの強制停止、無効化(Quarantine)、削除(Remove)、復元(Restore)を行います（誤操作防止の dry-run モード搭載）。


---

## 開発・ビルド環境の要件

このアプリケーションは C# (.NET 9) と Avalonia UI を使って開発されています。

- **[.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)**
- Windows / macOS / Linux (本手順は主に Windows での動作を想定しています)

---

## 事前準備（ADBツールの配置）

アプリケーションを実行・ビルドする前に、Android デバイスと通信するための **ADB (Android Debug Bridge)** ツールを定位置に配置する必要があります。

1. [Android SDK Platform-Tools](https://developer.android.com/studio/releases/platform-tools) をダウンロードし、解凍します。
2. 解凍したフォルダから以下の3つのファイルをコピーし、本プロジェクトの `NativeTools/Win/` フォルダ内に配置してください。
   - `NativeTools/Win/adb.exe`
   - `NativeTools/Win/AdbWinApi.dll`
   - `NativeTools/Win/AdbWinUsbApi.dll`

> ※ ビルド時、これらの ADBバイナリは `EmbeddedResource` としてアプリ内部に組み込まれます。

---

## ビルド・実行手順


### 1. 開発モードでの直接実行
コードの編集やテストを行う場合は、プロジェクトディレクトリ（`.csproj` のある場所）で以下のコマンドを実行します。
```bash
dotnet run
```
※ 初回実行時に NuGet パッケージの復元（Restore）およびビルドが自動で行われます。

### 2. 配布用バイナリのビルド（発行）
他の PC（.NET ランタイムがインストールされていない環境）に配布するために、単一の実行ファイル（`.exe`）としてビルドする場合は以下のコマンドを使用します。

```bash
# Windows x64 向けに単一の自己完結型 (.exe) アプリとして発行する
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

**出力先**:
コマンド実行後、成功すると以下のフォルダ内に `AdwScanGui.exe` が生成されます。
`bin/Release/net9.0/win-x64/publish/`

この `AdwScanGui.exe` をコピーするだけで、別の Windows PC でもそのまま起動・利用可能です。

---

## トラブルシューティング
- **ADBが認識されない・エラーになる**
  - 対象の Android 端末の「開発者向けオプション」から「USBデバッグ」がオンになっていることを確認してください。
  - PC に接続した際に表示される「USBデバッグを許可しますか？」のダイアログで許可を行ってください。
- **ビルド時のパッケージ復元エラー**
  - 万が一、内部ネットワーク等の問題で NuGet パッケージが取得できない場合は、プロジェクト内の `nuget.config` により公式ソースが指定されていることを確認し、手動で `dotnet restore` を再実行してください。
