using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdwScanGui.Models;
using AdwScanGui.Services;

namespace AdwScanGui.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly AdbService     _adb;
    private readonly StateService   _state;
    private readonly ScannerService _scanner;

    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _statusText = "アプリ一覧を更新してください";

    public ObservableCollection<PackageItem> Packages { get; } = [];

    // 選択パッケージを外部から購読可能にする
    public event Action<string>? InspectRequested;

    public InventoryViewModel(AdbService adb, StateService state, ScannerService scanner)
    {
        _adb     = adb;
        _state   = state;
        _scanner = scanner;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy     = true;
        StatusText = "一覧を取得中...";
        Packages.Clear();

        try
        {
            var pkgs = await _adb.ListUserPackagesAsync();
            StatusText = $"{pkgs.Count} 件のユーザーアプリ";
            foreach (var pkg in pkgs)
            {
                var actioned = _state.IsActioned(pkg);
                var item = new PackageItem(pkg, actioned ? "対処済み" : "未評価", ScoreLevel.Low, -1);
                Packages.Add(item);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"エラー: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Inspect(PackageItem? item)
    {
        if (item == null) return;
        InspectRequested?.Invoke(item.Package);
    }
}

public record PackageItem(string Package, string Status, ScoreLevel Level, int Score)
{
    public string ScoreLabel => Score < 0 ? "—" : Score.ToString();
}
