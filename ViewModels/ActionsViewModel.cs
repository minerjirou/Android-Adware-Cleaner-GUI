using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdwScanGui.Services;

namespace AdwScanGui.ViewModels;

public partial class ActionsViewModel : ObservableObject
{
    private readonly AdbService   _adb;
    private readonly StateService _state;

    [ObservableProperty] private string _packageName = "";
    [ObservableProperty] private bool   _dryRun = true;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _log = "";

    public ActionsViewModel(AdbService adb, StateService state)
    {
        _adb   = adb;
        _state = state;
    }

    public void SetPackage(string pkg) => PackageName = pkg;

    private void AppendLog(string msg)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            Log = Log + $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
    }

    [RelayCommand]
    private async Task QuarantineAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageName)) return;
        IsBusy = true;
        var pkg = PackageName;
        try
        {
            AppendLog($"force-stop {pkg}" + (DryRun ? " (dry-run)" : ""));
            if (!DryRun) await _adb.ForceStopAsync(pkg);

            AppendLog($"disable-user --user 0 {pkg}" + (DryRun ? " (dry-run)" : ""));
            if (!DryRun) await _adb.DisableUserAsync(pkg);

            if (!DryRun)
                _state.RecordAction(pkg, "quarantine", -1, []);
            AppendLog("✅ 隔離完了");
        }
        catch (Exception ex) { AppendLog($"❌ エラー: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageName)) return;
        IsBusy = true;
        var pkg = PackageName;
        try
        {
            AppendLog($"force-stop {pkg}" + (DryRun ? " (dry-run)" : ""));
            if (!DryRun) await _adb.ForceStopAsync(pkg);

            AppendLog($"disable-user --user 0 {pkg}" + (DryRun ? " (dry-run)" : ""));
            if (!DryRun) await _adb.DisableUserAsync(pkg);

            AppendLog($"uninstall --user 0 {pkg}" + (DryRun ? " (dry-run)" : ""));
            if (!DryRun) await _adb.UninstallUser0Async(pkg);

            if (!DryRun)
                _state.RecordAction(pkg, "remove", -1, []);
            AppendLog("✅ 削除完了");
        }
        catch (Exception ex) { AppendLog($"❌ エラー: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageName)) return;
        IsBusy = true;
        var pkg = PackageName;
        try
        {
            AppendLog($"enable --user 0 {pkg}" + (DryRun ? " (dry-run)" : ""));
            if (!DryRun) await _adb.EnableUser0Async(pkg);

            AppendLog($"install-existing {pkg}" + (DryRun ? " (dry-run)" : ""));
            if (!DryRun) await _adb.InstallExistingAsync(pkg);

            AppendLog("✅ 復元完了");
        }
        catch (Exception ex) { AppendLog($"❌ エラー: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ClearLog() => Log = "";
}
