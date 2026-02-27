using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdwScanGui.Models;
using AdwScanGui.Services;

namespace AdwScanGui.ViewModels;

public partial class MonitorViewModel : ObservableObject
{
    private readonly AdbService   _adb;
    private readonly StateService _state;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool   _isMonitoring;
    [ObservableProperty] private string _statusText = "停止中";
    [ObservableProperty] private bool   _autoScroll  = true;

    public ObservableCollection<EventRow> Events { get; } = [];

    public MonitorViewModel(AdbService adb, StateService state)
    {
        _adb   = adb;
        _state = state;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsMonitoring) return;
        _cts = new CancellationTokenSource();
        IsMonitoring = true;
        StatusText   = "監視中...";

        try
        {
            await foreach (var evt in _adb.StreamDisplayedEventsAsync(clear: false, _cts.Token))
            {
                _state.PushEvent(evt);
                _state.UpdateLastSeen(evt.Package, evt.Timestamp);
                _state.AppendEventLog(evt);

                var cnt = _state.CountRecent(evt.Package, 600);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Events.Add(new EventRow(evt, cnt));
                    // 5000行を超えたら古いのを削除
                    while (Events.Count > 5000) Events.RemoveAt(0);
                });
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsMonitoring = false;
            StatusText   = "停止中";
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void Clear() => Events.Clear();
}

public record EventRow(Event Evt, int Count10m)
{
    public string Time     => Evt.TimeFormatted;
    public string Package  => Evt.Package;
    public string Activity => Evt.Activity;
    public string CountLabel => $"10m={Count10m}";
    public ScoreLevel Level  => Count10m switch
    {
        >= 8 => ScoreLevel.Critical,
        >= 5 => ScoreLevel.High,
        >= 3 => ScoreLevel.Medium,
        _    => ScoreLevel.Low,
    };
}
