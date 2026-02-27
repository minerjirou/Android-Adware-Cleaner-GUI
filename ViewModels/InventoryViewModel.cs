using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
            StatusText = $"{pkgs.Count} 件のユーザーアプリ（名前解決中...）";
            foreach (var pkg in pkgs)
            {
                var actioned = _state.IsActioned(pkg);
                var label = await _adb.GetPackageLabelAsync(pkg);
                var displayName = string.IsNullOrWhiteSpace(label) ? pkg : label;

                var item = new PackageItem(
                    pkg,
                    displayName,
                    actioned ? "対処済み" : "未評価",
                    ScoreLevel.Low,
                    -1);
                Packages.Add(item);
            }

            StatusText = $"{pkgs.Count} 件のユーザーアプリ";
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

public record PackageItem(string Package, string DisplayName, string Status, ScoreLevel Level, int Score)
{
    public string ScoreLabel => Score < 0 ? "—" : Score.ToString();
}
