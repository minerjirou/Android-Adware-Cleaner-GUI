namespace AdwScanGui.Models;

public record ScoreResult(
    string Package,
    int Score,
    List<string> Reasons,
    Dictionary<string, object?> Details)
{
    public ScoreLevel Level => Score switch
    {
        >= 105 => ScoreLevel.Critical,
        >= 80  => ScoreLevel.High,
        >= 45  => ScoreLevel.Medium,
        _      => ScoreLevel.Low
    };
}

public enum ScoreLevel { Low, Medium, High, Critical }
