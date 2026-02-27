using System;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdwScanGui.Services;

namespace AdwScanGui.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public AdbService     Adb     { get; }
    public StateService   State   { get; }
    public ScannerService Scanner { get; }
    public PlayStoreService PlayStore { get; }

    public MonitorViewModel   Monitor   { get; }
    public InventoryViewModel Inventory { get; }
    public InspectViewModel   Inspect   { get; }
    public ActionsViewModel   Actions   { get; }

    [ObservableProperty] private int    _selectedTabIndex;
    [ObservableProperty] private string _deviceStatus = "デバイス未接続";
    [ObservableProperty] private bool   _deviceConnected;
    [ObservableProperty] private object? _currentView;

    public MainWindowViewModel()
    {
        Adb     = new AdbService();
        State   = new StateService();
        Scanner = new ScannerService();
        PlayStore = new PlayStoreService(new HttpClient());

        Monitor   = new MonitorViewModel(Adb, State);
        Inventory = new InventoryViewModel(Adb, State, Scanner, PlayStore);
        Inspect   = new InspectViewModel(Adb, State, Scanner);
        Actions   = new ActionsViewModel(Adb, State);

        // Inventory → Inspect 遷移
        Inventory.InspectRequested += pkg =>
        {
            Inspect.SetPackage(pkg);
            Actions.SetPackage(pkg);
            SelectedTabIndex = 2; // Inspect タブへ
            CurrentView = Inspect;
        };

        // Inspect → Actions 遷移
        Inspect.ActionRequested += action =>
        {
            SelectedTabIndex = 3; // Actions タブへ
            CurrentView = Actions;
        };

        // バックグラウンドでデバイス状態を定期確認
        _ = PollDeviceStatusAsync();
        CurrentView = Monitor; // 初期タブ
    }

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

    private async Task PollDeviceStatusAsync()
    {
        while (true)
        {
            try
            {
                var ready = await Adb.IsDeviceReadyAsync();
                if (ready)
                {
                    var devices = await Adb.GetDeviceListAsync();
                    var serial  = devices.FirstOrDefault() ?? "device";
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        DeviceConnected = true;
                        DeviceStatus    = $"✅ {serial}";
                    });
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        DeviceConnected = false;
                        DeviceStatus    = "⚠ デバイス未接続";
                    });
                }
            }
            catch
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    DeviceConnected = false;
                    DeviceStatus    = "⚠ ADB エラー";
                });
            }
            await Task.Delay(5000);
        }
    }
}
