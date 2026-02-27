using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdwScanGui.Models;
using AdwScanGui.Services;

namespace AdwScanGui.ViewModels;

public partial class InspectViewModel : ObservableObject
{
    private readonly AdbService     _adb;
    private readonly StateService   _state;
    private readonly ScannerService _scanner;

    [ObservableProperty] private string  _packageName = "";
    [ObservableProperty] private bool    _isBusy;
    [ObservableProperty] private int     _score;
    [ObservableProperty] private string  _scoreText    = "—";
    [ObservableProperty] private string  _levelLabel   = "";
    [ObservableProperty] private string  _reasonsText  = "";
    [ObservableProperty] private string  _detailsText  = "";
    [ObservableProperty] private double  _scoreFraction; // 0.0 〜 1.0
    [ObservableProperty] private ScoreLevel _level = ScoreLevel.Low;
    [ObservableProperty] private bool    _hasResult;

    public ScoreResult? LastResult { get; private set; }
    public event Action<string>? ActionRequested;

    public InspectViewModel(AdbService adb, StateService state, ScannerService scanner)
    {
        _adb     = adb;
        _state   = state;
        _scanner = scanner;
    }

    public void SetPackage(string pkg)
    {
        PackageName = pkg;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageName)) return;
        IsBusy    = true;
        HasResult = false;
        ReasonText = "";
        DetailsText = "";

        try
        {
            var recent = _state.CountRecent(PackageName, 600);
            var result = await _scanner.ScorePackageAsync(_adb, PackageName, recent,
                _state.GetAllowlistExact(), _state.GetAllowlistPrefixes());

            LastResult    = result;
            Score         = result.Score;
            ScoreText     = result.Score.ToString();
            ScoreFraction = Math.Min(result.Score / 180.0, 1.0);
            Level         = result.Level;
            LevelLabel    = result.Level switch
            {
                ScoreLevel.Critical => "危険",
                ScoreLevel.High     => "高",
                ScoreLevel.Medium   => "警告",
                _                   => "低"
            };
            ReasonsText = result.Reasons.Count > 0
                ? string.Join("\n", result.Reasons.Select(r => $"• {r}"))
                : "（シグナルなし）";

            DetailsText = string.Join("\n", result.Details
                .Where(kv => kv.Key != "pkg")
                .Select(kv => $"{kv.Key}: {kv.Value}"));

            HasResult = true;
        }
        catch (Exception ex)
        {
            ReasonsText = $"エラー: {ex.Message}";
            HasResult   = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void RequestAction(string action) => ActionRequested?.Invoke(action);
}
