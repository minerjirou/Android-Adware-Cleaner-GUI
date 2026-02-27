# AdwScan GUI — 実装引き継ぎ指示書

> **対象AI向け**: このドキュメントは、C# + Avalonia UI 11 によるクロスプラットフォーム Android アドウェアスキャナーGUI の実装を引き継ぐための完全な指示書です。

---

## プロジェクト概要

| 項目 | 内容 |
|------|------|
| プロジェクトパス | `C:\Users\Y_Kofuji\Documents\Android-Adware-Cleaner-GUI` |
| 言語 | C# (.NET 9) |
| UIフレームワーク | Avalonia UI 11.3.12 |
| MVVMライブラリ | CommunityToolkit.Mvvm 8.4.0 |
| 対応OS | Windows / macOS / Linux（クロスプラットフォーム） |

---

## 現在の実装状況（作成済み）

| ファイル | 状態 |
|---------|------|
| `AdwScanGui.csproj` | ✅ 完成 |
| `App.axaml` | ✅ 完成（ダークテーマ・スタイル定義済み） |
| `App.axaml.cs` | ✅ テンプレートのまま使用可（変更不要） |
| `Program.cs` | ✅ テンプレートのまま使用可（変更不要） |
| `ViewLocator.cs` | ⚠️ 要修正（後述） |
| `Models/Event.cs` | ✅ 完成 |
| `Models/ScoreResult.cs` | ✅ 完成 |
| `Services/AdbService.cs` | ✅ 完成 |
| `Services/ScannerService.cs` | ✅ 完成 |
| `Services/StateService.cs` | ✅ 完成 |
| `ViewModels/MainWindowViewModel.cs` | ✅ 完成 |
| `ViewModels/MonitorViewModel.cs` | ✅ 完成 |
| `ViewModels/InventoryViewModel.cs` | ✅ 完成 |
| `ViewModels/InspectViewModel.cs` | ✅ 完成（後述のタイポ修正が必要） |
| `ViewModels/ActionsViewModel.cs` | ✅ 完成 |
| `Views/MainWindow.axaml` | ❌ 要完全書き直し（後述） |
| `Views/MainWindow.axaml.cs` | ❌ 要作成 |
| `Views/MonitorView.axaml` | ❌ 要作成 |
| `Views/MonitorView.axaml.cs` | ❌ 要作成 |
| `Views/InventoryView.axaml` | ❌ 要作成 |
| `Views/InventoryView.axaml.cs` | ❌ 要作成 |
| `Views/InspectView.axaml` | ❌ 要作成 |
| `Views/InspectView.axaml.cs` | ❌ 要作成 |
| `Views/ActionsView.axaml` | ❌ 要作成 |
| `Views/ActionsView.axaml.cs` | ❌ 要作成 |
| `NativeTools/README.md` | ❌ 要作成（ADB配置手順） |

---

## 1. 最初に行う修正

### 1-1. `InspectViewModel.cs` のタイポ修正

`ReasonText` → `ReasonsText`、`ReasonText = ""` となっている行を修正する。

```csharp
// 誤（現在）
ReasonText = "";

// 正
ReasonsText = "";
DetailsText = "";
```

### 1-2. `ViewLocator.cs` の修正

`Match` メソッドが `ViewModelBase` を継承しているかチェックしているが、
今回の ViewModel は `ObservableObject` を継承しているため、`object` に変更する。

```csharp
// 現在
public bool Match(object? data)
{
    return data is ViewModelBase;
}

// 修正後
public bool Match(object? data)
{
    return data is not null;
}
```

---

## 2. `MainWindowViewModel.cs` への追加（タブナビゲーション）

現在の `MainWindowViewModel.cs` には `CurrentView` と `SelectTabCommand` が**不足**している。
以下のプロパティ・コマンドを追加すること。

```csharp
// [ObservableProperty] の下に追加
[ObservableProperty] private object? _currentView;

// コンストラクタの最後に追加
CurrentView = Monitor; // 初期タブ

// RelayCommand として追加
[RelayCommand]
private void SelectTab(string indexStr)
{
    SelectedTabIndex = int.Parse(indexStr);
    CurrentView = SelectedTabIndex switch
    {
        0 => Monitor,
        1 => Inventory,
        2 => Inspect,
        3 => Actions,
        _ => Monitor,
    };
}
```

---

## 3. `Views/MainWindow.axaml` の完全書き直し

現在の MainWindow.axaml を**丸ごと以下で置き換える**。
TransitioningContentControl + CrossFade でタブ切り替えアニメーションを実現する。

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:AdwScanGui.ViewModels"
        x:Class="AdwScanGui.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="AdwScan — Android Adware Cleaner"
        Width="1100" Height="720"
        MinWidth="800" MinHeight="560"
        Background="{StaticResource BgBaseBrush}">

    <Grid ColumnDefinitions="220,*">

        <!-- ═══ Sidebar ═══ -->
        <Border Grid.Column="0"
                Background="{StaticResource BgSurfaceBrush}"
                BorderBrush="#1E2230" BorderThickness="0,0,1,0">
            <DockPanel Margin="12">

                <!-- タイトル -->
                <StackPanel DockPanel.Dock="Top" Margin="0,8,0,28">
                    <TextBlock Text="🤖 AdwScan"
                               FontSize="20" FontWeight="Bold"
                               Foreground="{StaticResource AccentBrush}"/>
                    <TextBlock Text="Android Adware Cleaner"
                               FontSize="11"
                               Foreground="{StaticResource TextSecondaryBrush}"
                               Margin="0,2,0,0"/>
                </StackPanel>

                <!-- デバイス状態 -->
                <Border DockPanel.Dock="Bottom"
                        Background="{StaticResource BgElevatedBrush}"
                        CornerRadius="10" Padding="12,10" Margin="0,12,0,0">
                    <StackPanel>
                        <TextBlock Text="デバイス状態" FontSize="10"
                                   Foreground="{StaticResource TextSecondaryBrush}"
                                   Margin="0,0,0,4"/>
                        <TextBlock Text="{Binding DeviceStatus}"
                                   FontSize="12" FontWeight="Medium"
                                   Foreground="{StaticResource TextPrimaryBrush}"
                                   TextWrapping="Wrap"/>
                    </StackPanel>
                </Border>

                <!-- ナビゲーション -->
                <StackPanel Spacing="4">
                    <Button Classes="nav" Command="{Binding SelectTabCommand}" CommandParameter="0">
                        <StackPanel Orientation="Horizontal" Spacing="10">
                            <TextBlock Text="📡" FontSize="16"/>
                            <TextBlock Text="Monitor" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Classes="nav" Command="{Binding SelectTabCommand}" CommandParameter="1">
                        <StackPanel Orientation="Horizontal" Spacing="10">
                            <TextBlock Text="📋" FontSize="16"/>
                            <TextBlock Text="Inventory" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Classes="nav" Command="{Binding SelectTabCommand}" CommandParameter="2">
                        <StackPanel Orientation="Horizontal" Spacing="10">
                            <TextBlock Text="🔍" FontSize="16"/>
                            <TextBlock Text="Inspect" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                    <Button Classes="nav" Command="{Binding SelectTabCommand}" CommandParameter="3">
                        <StackPanel Orientation="Horizontal" Spacing="10">
                            <TextBlock Text="⚡" FontSize="16"/>
                            <TextBlock Text="Actions" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                </StackPanel>
            </DockPanel>
        </Border>

        <!-- ═══ Content ═══ -->
        <TransitioningContentControl Grid.Column="1"
                                     Content="{Binding CurrentView}">
            <TransitioningContentControl.PageTransition>
                <CrossFade Duration="0:0:0.2"/>
            </TransitioningContentControl.PageTransition>
        </TransitioningContentControl>
    </Grid>
</Window>
```

### `Views/MainWindow.axaml.cs`（code-behind）

```csharp
using Avalonia.Controls;

namespace AdwScanGui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

---

## 4. Views の作成

### 4-1. `Views/MonitorView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:AdwScanGui.ViewModels"
             x:Class="AdwScanGui.Views.MonitorView"
             x:DataType="vm:MonitorViewModel">
    <DockPanel Margin="24">
        <!-- ヘッダー -->
        <DockPanel DockPanel.Dock="Top" Margin="0,0,0,16">
            <TextBlock Text="📡 Monitor" FontSize="22" FontWeight="Bold"
                       Foreground="{StaticResource TextPrimaryBrush}"
                       VerticalAlignment="Center"/>
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8">
                <Button Classes="primary" Content="▶ 開始"
                        Command="{Binding StartCommand}"
                        IsEnabled="{Binding !IsMonitoring}"/>
                <Button Classes="outline" Content="■ 停止"
                        Command="{Binding StopCommand}"
                        IsEnabled="{Binding IsMonitoring}"/>
                <Button Classes="outline" Content="クリア"
                        Command="{Binding ClearCommand}"/>
            </StackPanel>
        </DockPanel>

        <!-- ステータス -->
        <TextBlock DockPanel.Dock="Top"
                   Text="{Binding StatusText}"
                   Foreground="{StaticResource TextSecondaryBrush}"
                   FontSize="12" Margin="0,0,0,12"/>

        <!-- ログリスト -->
        <Border Classes="card" DockPanel.Dock="Top" Padding="0">
            <ListBox ItemsSource="{Binding Events}"
                     Background="Transparent"
                     ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                <ListBox.ItemTemplate>
                    <DataTemplate x:DataType="vm:EventRow">
                        <Grid ColumnDefinitions="160,*,80" Margin="8,4">
                            <TextBlock Grid.Column="0"
                                       Text="{Binding Time}"
                                       FontSize="11" FontFamily="Consolas, monospace"
                                       Foreground="{StaticResource TextSecondaryBrush}"
                                       VerticalAlignment="Center"/>
                            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8">
                                <TextBlock Text="{Binding Package}"
                                           FontSize="12" FontWeight="SemiBold"
                                           Foreground="{StaticResource TextPrimaryBrush}"
                                           VerticalAlignment="Center"/>
                                <TextBlock Text="{Binding Activity}"
                                           FontSize="11"
                                           Foreground="{StaticResource TextSecondaryBrush}"
                                           VerticalAlignment="Center"
                                           TextTrimming="CharacterEllipsis"
                                           MaxWidth="300"/>
                            </StackPanel>
                            <Border Grid.Column="2" CornerRadius="6" Padding="6,2"
                                    HorizontalAlignment="Right" VerticalAlignment="Center">
                                <Border.Background>
                                    <!-- CountLabel に応じて色を変える（シンプル化のためTextBlockで代用）-->
                                    <SolidColorBrush Color="#2A2F42"/>
                                </Border.Background>
                                <TextBlock Text="{Binding CountLabel}"
                                           FontSize="11"
                                           Foreground="{StaticResource AccentBrush}"/>
                            </Border>
                        </Grid>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </Border>
    </DockPanel>
</UserControl>
```

### `Views/MonitorView.axaml.cs`

```csharp
using Avalonia.Controls;

namespace AdwScanGui.Views;

public partial class MonitorView : UserControl
{
    public MonitorView() => InitializeComponent();
}
```

---

### 4-2. `Views/InventoryView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:AdwScanGui.ViewModels"
             x:Class="AdwScanGui.Views.InventoryView"
             x:DataType="vm:InventoryViewModel">
    <DockPanel Margin="24">
        <!-- ヘッダー -->
        <DockPanel DockPanel.Dock="Top" Margin="0,0,0,16">
            <TextBlock Text="📋 Inventory" FontSize="22" FontWeight="Bold"
                       Foreground="{StaticResource TextPrimaryBrush}"
                       VerticalAlignment="Center"/>
            <Button Classes="primary" Content="🔄 一覧更新"
                    Command="{Binding RefreshCommand}"
                    HorizontalAlignment="Right"/>
        </DockPanel>

        <!-- ステータス -->
        <TextBlock DockPanel.Dock="Top"
                   Text="{Binding StatusText}"
                   Foreground="{StaticResource TextSecondaryBrush}"
                   FontSize="12" Margin="0,0,0,12"/>

        <!-- ローディング -->
        <ProgressBar DockPanel.Dock="Top"
                     IsIndeterminate="True"
                     IsVisible="{Binding IsBusy}"
                     Margin="0,0,0,12" Height="3"/>

        <!-- パッケージ一覧 -->
        <Border Classes="card" Padding="0">
            <ListBox ItemsSource="{Binding Packages}"
                     Background="Transparent">
                <ListBox.ItemTemplate>
                    <DataTemplate x:DataType="vm:PackageItem">
                        <Grid ColumnDefinitions="*,80,100" Margin="12,6">
                            <TextBlock Grid.Column="0"
                                       Text="{Binding Package}"
                                       FontSize="13"
                                       Foreground="{StaticResource TextPrimaryBrush}"
                                       VerticalAlignment="Center"
                                       TextTrimming="CharacterEllipsis"/>
                            <TextBlock Grid.Column="1"
                                       Text="{Binding Status}"
                                       FontSize="11"
                                       Foreground="{StaticResource TextSecondaryBrush}"
                                       VerticalAlignment="Center"
                                       HorizontalAlignment="Center"/>
                            <Button Grid.Column="2"
                                    Classes="outline"
                                    Content="🔍 Inspect"
                                    FontSize="11" Padding="8,4"
                                    Command="{Binding $parent[ListBox].((vm:InventoryViewModel)DataContext).InspectCommand}"
                                    CommandParameter="{Binding}"/>
                        </Grid>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </Border>
    </DockPanel>
</UserControl>
```

### `Views/InventoryView.axaml.cs`

```csharp
using Avalonia.Controls;

namespace AdwScanGui.Views;

public partial class InventoryView : UserControl
{
    public InventoryView() => InitializeComponent();
}
```

---

### 4-3. `Views/InspectView.axaml`

スコアメーターはシンプルな `ProgressBar` + アニメーション付き数値表示で実装する。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:AdwScanGui.ViewModels"
             x:Class="AdwScanGui.Views.InspectView"
             x:DataType="vm:InspectViewModel">
    <ScrollViewer>
        <StackPanel Margin="24" Spacing="16">
            <!-- ヘッダー -->
            <TextBlock Text="🔍 Inspect" FontSize="22" FontWeight="Bold"
                       Foreground="{StaticResource TextPrimaryBrush}"/>

            <!-- パッケージ入力 -->
            <Border Classes="card">
                <StackPanel Spacing="10">
                    <TextBlock Text="パッケージ名" FontSize="12"
                               Foreground="{StaticResource TextSecondaryBrush}"/>
                    <Grid ColumnDefinitions="*,Auto">
                        <TextBox Grid.Column="0"
                                 Text="{Binding PackageName}"
                                 Watermark="例: com.example.suspicious"
                                 Margin="0,0,8,0"/>
                        <Button Grid.Column="1" Classes="primary"
                                Content="🔍 スキャン"
                                Command="{Binding ScanCommand}"
                                IsEnabled="{Binding !IsBusy}"/>
                    </Grid>
                    <ProgressBar IsIndeterminate="True"
                                 IsVisible="{Binding IsBusy}"
                                 Height="3"/>
                </StackPanel>
            </Border>

            <!-- スコア表示 -->
            <Border Classes="card" IsVisible="{Binding HasResult}">
                <Grid ColumnDefinitions="*,200">
                    <StackPanel Grid.Column="0" Spacing="8">
                        <TextBlock Text="スコア" FontSize="12"
                                   Foreground="{StaticResource TextSecondaryBrush}"/>
                        <TextBlock Text="{Binding ScoreText}"
                                   FontSize="48" FontWeight="Bold"
                                   Foreground="{StaticResource AccentBrush}"/>
                        <TextBlock Text="{Binding LevelLabel}"
                                   FontSize="14" FontWeight="SemiBold"
                                   Foreground="{StaticResource AccentBrush}"/>
                    </StackPanel>
                    <!-- スコアバー -->
                    <StackPanel Grid.Column="1" VerticalAlignment="Center" Spacing="8">
                        <TextBlock Text="危険度メーター" FontSize="11"
                                   Foreground="{StaticResource TextSecondaryBrush}"/>
                        <ProgressBar Value="{Binding ScoreFraction}"
                                     Minimum="0" Maximum="1"
                                     Height="12" CornerRadius="6">
                            <ProgressBar.Transitions>
                                <Transitions>
                                    <DoubleTransition Property="Value" Duration="0:0:0.6"/>
                                </Transitions>
                            </ProgressBar.Transitions>
                        </ProgressBar>
                        <Grid ColumnDefinitions="*,*,*,*">
                            <TextBlock Grid.Column="0" Text="低" FontSize="10"
                                       Foreground="{StaticResource TextSecondaryBrush}"/>
                            <TextBlock Grid.Column="3" Text="危険" FontSize="10"
                                       HorizontalAlignment="Right"
                                       Foreground="{StaticResource TextSecondaryBrush}"/>
                        </Grid>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- 理由リスト -->
            <Border Classes="card" IsVisible="{Binding HasResult}">
                <StackPanel Spacing="8">
                    <TextBlock Text="検出シグナル" FontSize="14" FontWeight="SemiBold"
                               Foreground="{StaticResource TextPrimaryBrush}"/>
                    <TextBlock Text="{Binding ReasonsText}"
                               FontSize="12" TextWrapping="Wrap"
                               Foreground="{StaticResource TextPrimaryBrush}"
                               FontFamily="Consolas, monospace"/>
                </StackPanel>
            </Border>

            <!-- アクションボタン -->
            <StackPanel Orientation="Horizontal" Spacing="10"
                        IsVisible="{Binding HasResult}">
                <Button Classes="outline" Content="⚡ Actions へ"
                        Command="{Binding RequestActionCommand}"
                        CommandParameter="goto"/>
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

### `Views/InspectView.axaml.cs`

```csharp
using Avalonia.Controls;

namespace AdwScanGui.Views;

public partial class InspectView : UserControl
{
    public InspectView() => InitializeComponent();
}
```

---

### 4-4. `Views/ActionsView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:AdwScanGui.ViewModels"
             x:Class="AdwScanGui.Views.ActionsView"
             x:DataType="vm:ActionsViewModel">
    <DockPanel Margin="24">
        <!-- ヘッダー -->
        <TextBlock DockPanel.Dock="Top"
                   Text="⚡ Actions" FontSize="22" FontWeight="Bold"
                   Foreground="{StaticResource TextPrimaryBrush}"
                   Margin="0,0,0,16"/>

        <!-- 操作パネル -->
        <Border Classes="card" DockPanel.Dock="Top" Margin="0,0,0,16">
            <StackPanel Spacing="12">
                <!-- パッケージ名 -->
                <TextBlock Text="対象パッケージ名" FontSize="12"
                           Foreground="{StaticResource TextSecondaryBrush}"/>
                <TextBox Text="{Binding PackageName}"
                         Watermark="例: com.example.suspicious"/>

                <!-- dry-run トグル -->
                <StackPanel Orientation="Horizontal" Spacing="10" Margin="0,4,0,0">
                    <ToggleSwitch IsChecked="{Binding DryRun}"
                                  OnContent="dry-run ON（実際には実行しない）"
                                  OffContent="実行モード（実際に操作）"/>
                </StackPanel>

                <!-- アクションボタン -->
                <WrapPanel Orientation="Horizontal">
                    <Button Classes="outline" Content="🔒 Quarantine"
                            Margin="0,0,8,0"
                            Command="{Binding QuarantineCommand}"
                            IsEnabled="{Binding !IsBusy}"/>
                    <Button Classes="danger" Content="🗑 Remove"
                            Margin="0,0,8,0"
                            Command="{Binding RemoveCommand}"
                            IsEnabled="{Binding !IsBusy}"/>
                    <Button Classes="outline" Content="♻ Restore"
                            Command="{Binding RestoreCommand}"
                            IsEnabled="{Binding !IsBusy}"/>
                </WrapPanel>

                <ProgressBar IsIndeterminate="True"
                             IsVisible="{Binding IsBusy}"
                             Height="3"/>
            </StackPanel>
        </Border>

        <!-- ログ出力 -->
        <Border Classes="card">
            <DockPanel>
                <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
                    <TextBlock Text="実行ログ" FontSize="13" FontWeight="SemiBold"
                               Foreground="{StaticResource TextPrimaryBrush}"
                               VerticalAlignment="Center"/>
                    <Button Classes="outline" Content="クリア"
                            Command="{Binding ClearLogCommand}"
                            HorizontalAlignment="Right"
                            FontSize="11" Padding="8,4"/>
                </DockPanel>
                <ScrollViewer>
                    <TextBlock Text="{Binding Log}"
                               FontFamily="Consolas, monospace"
                               FontSize="12"
                               Foreground="{StaticResource TextPrimaryBrush}"
                               TextWrapping="Wrap"/>
                </ScrollViewer>
            </DockPanel>
        </Border>
    </DockPanel>
</UserControl>
```

### `Views/ActionsView.axaml.cs`

```csharp
using Avalonia.Controls;

namespace AdwScanGui.Views;

public partial class ActionsView : UserControl
{
    public ActionsView() => InitializeComponent();
}
```

---

## 5. ViewLocator の更新

`MainWindowViewModel.CurrentView` に各 ViewModel を返すため、ViewLocator の `Match` を修正する（前述の通り）。

また、`ViewLocator.cs` の `Build` メソッドのクラス名変換ロジックが
`ViewModels.Xxx` → `Views.Xxx` に変換するかを確認する。

現在の変換ロジック:
```
AdwScanGui.ViewModels.MonitorViewModel → AdwScanGui.Views.MonitorView ✅
```
これは正しく機能するので `Build` は変更不要。

---

## 6. NativeTools の準備

以下のフォルダ構成で ADB バイナリを配置する指示をREADMEに記載。

```
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

**ダウンロード先**: https://developer.android.com/tools/releases/platform-tools

バイナリが存在しない場合、`AdbService` は自動的に PATH 上の `adb` を使用する（フォールバック実装済み）。

---

## 7. ビルド確認コマンド

```powershell
cd c:\Users\Y_Kofuji\Documents\Android-Adware-Cleaner\AdwScanGui
dotnet restore
dotnet build
```

エラーがなければ:

```powershell
dotnet run
```

---

## 8. デザインガイドライン（App.axaml に定義済み）

| トークン | 用途 |
|---------|------|
| `BgBaseBrush` (`#0F1117`) | ウィンドウ背景 |
| `BgSurfaceBrush` (`#1A1D27`) | サイドバー・カード背景 |
| `BgElevatedBrush` (`#242836`) | 入力欄・リスト背景 |
| `AccentBrush` (`#3DDC84`) | Androidグリーン・アクセント |
| `TextPrimaryBrush` (`#E8EAF0`) | メインテキスト |
| `TextSecondaryBrush` (`#8B90A0`) | サブテキスト・ラベル |

**定義済みスタイルクラス**:
- `Button.primary` — 緑のプライマリボタン（ホバーでスケールアップ）
- `Button.danger` — 赤の危険ボタン
- `Button.outline` — アウトラインボタン
- `Button.nav` — サイドバーナビゲーションボタン
- `Border.card` — ダークカードパネル

---

## 9. 注意事項

- **CompileBindings**: `App.axaml` で `AvaloniaUseCompiledBindingsByDefault=true` が設定されているため、
  各 AXAML ファイルに `x:DataType` を必ず指定すること。

- **リスト内バインディング**: `ListBox.ItemTemplate` 内から親 ViewModel にコマンドバインドする場合は
  `$parent[ListBox].((vm:InventoryViewModel)DataContext).XxxCommand` のように型キャストを使う。

- **UIスレッド**: バックグラウンドスレッドからUIを更新する場合は必ず
  `Avalonia.Threading.Dispatcher.UIThread.Post(...)` を使用する（AdbService / MonitorViewModel に実装済み）。

- **CancellationToken**: MonitorViewModel の `StartAsync` はキャンセル可能。
  `Stop` ボタンで `_cts.Cancel()` を呼ぶことで logcat プロセスも終了する。
