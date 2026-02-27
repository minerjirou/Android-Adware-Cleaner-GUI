using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AdwScanGui.Services;

/// <summary>
/// Google Play ストアページから表示名を取得する（失敗時は null）
/// </summary>
public sealed class PlayStoreService : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.Ordinal);

    private static readonly Regex OgTitleRe = new(
        "<meta\\s+property=\"og:title\"\\s+content=\"(?<title>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleTagRe = new(
        "<title>(?<title>.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public PlayStoreService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _ownsHttpClient = http is null;

        _http.Timeout = TimeSpan.FromSeconds(5);

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.TryParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AdwScanGui/1.0");
        }
    }

    public async Task<string?> GetAppNameAsync(
        string packageName,
        string hl = "ja",
        string gl = "JP",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return null;

        var cacheKey = $"{packageName}|{hl}|{gl}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var url =
                $"https://play.google.com/store/apps/details?id={Uri.EscapeDataString(packageName)}" +
                $"&hl={Uri.EscapeDataString(hl)}&gl={Uri.EscapeDataString(gl)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept-Language", "ja-JP,ja;q=0.9,en;q=0.8");

            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
            {
                _cache[cacheKey] = null;
                return null;
            }

            var html = await res.Content.ReadAsStringAsync(ct);

            var m1 = OgTitleRe.Match(html);
            if (m1.Success)
            {
                var title = NormalizePlayTitle(m1.Groups["title"].Value);
                _cache[cacheKey] = title;
                return title;
            }

            var m2 = TitleTagRe.Match(html);
            if (m2.Success)
            {
                var title = NormalizePlayTitle(m2.Groups["title"].Value);
                _cache[cacheKey] = title;
                return title;
            }
        }
        catch
        {
            // 通信失敗・タイムアウト・HTML構造変更時は null を返してフォールバック
        }

        _cache[cacheKey] = null;
        return null;
    }

    private static string? NormalizePlayTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = System.Net.WebUtility.HtmlDecode(raw).Trim();
        s = s.Replace(" - Google Play のアプリ", "", StringComparison.Ordinal);
        s = s.Replace(" - Apps on Google Play", "", StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
}
