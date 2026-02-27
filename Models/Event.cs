namespace AdwScanGui.Models;

public record Event(
    double Timestamp,
    string Package,
    string Activity,
    string Raw)
{
    public string TimeFormatted =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)(Timestamp * 1000))
            .LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
}
