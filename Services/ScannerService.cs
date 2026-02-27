using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AdwScanGui.Models;

namespace AdwScanGui.Services;

/// <summary>
/// adwscan.py のスコアリングロジックを C# に移植したサービス
/// </summary>
public class ScannerService
{
    // ── 閾値 ──────────────────────────────────────────────────────
    public int WarnThreshold   { get; set; } = 45;
    public int QuarThreshold   { get; set; } = 80;
    public int RemoveThreshold { get; set; } = 105;

    // ── Allowlist ─────────────────────────────────────────────────
    private static readonly string[] BuiltinPrefixes =
    [
        "com.android.", "com.google.android.", "com.google.", "android."
    ];
    private static readonly HashSet<string> BuiltinExact =
    [
        "com.android.systemui", "com.android.settings",
        "com.google.android.gms", "com.google.android.gsf",
        "com.google.android.packageinstaller", "com.android.packageinstaller"
    ];

    private static readonly Regex SuspiciousNameRe = new(
        @"(ad|ads|advert|offer|promo|boost|cleaner|junk|speed|battery|vpn|browser)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── 権限シグナル ───────────────────────────────────────────────
    private static readonly Dictionary<string, int> PermSignals = new()
    {
        ["SYSTEM_ALERT_WINDOW"]       = 35,
        ["BIND_ACCESSIBILITY_SERVICE"]= 35,
        ["PACKAGE_USAGE_STATS"]       = 15,
        ["RECEIVE_BOOT_COMPLETED"]    = 12,
        ["FOREGROUND_SERVICE"]        = 8,
        ["POST_NOTIFICATIONS"]        = 5,
        ["WAKE_LOCK"]                 = 5,
        ["REQUEST_INSTALL_PACKAGES"]  = 20,
    };

    private static readonly Dictionary<string, int> AppopsSignals = new()
    {
        ["SYSTEM_ALERT_WINDOW"]      = 25,
        ["GET_USAGE_STATS"]          = 15,
        ["REQUEST_INSTALL_PACKAGES"] = 10,
    };

    // ── 挙動テキストシグナル ───────────────────────────────────────
    private static readonly Dictionary<string, (string Label, int Pts)> BehaviorTextSignals = new()
    {
        ["android.intent.action.BOOT_COMPLETED"]    = ("BOOT_COMPLETED receiver/intent", 15),
        ["android.intent.action.USER_PRESENT"]      = ("USER_PRESENT receiver/intent", 20),
        ["INSTALL_SHORTCUT"]                         = ("shortcut creation behavior", 10),
        ["android.intent.action.PACKAGE_ADDED"]     = ("PACKAGE_ADDED receiver/intent", 8),
        ["android.intent.action.PACKAGE_REPLACED"]  = ("PACKAGE_REPLACED receiver/intent", 8),
    };

    private static readonly HashSet<string> TrustedInstallers =
    [
        "com.android.vending",
        "com.google.android.packageinstaller",
        "com.android.packageinstaller",
        "com.sec.android.app.samsungapps",
        "com.huawei.appmarket",
        "com.xiaomi.market",
    ];

    // ── Public API ────────────────────────────────────────────────
    public bool IsAllowlisted(string pkg, HashSet<string>? customExact = null, List<string>? customPrefixes = null)
    {
        var exact    = customExact    ?? BuiltinExact;
        var prefixes = customPrefixes ?? [.. BuiltinPrefixes];
        if (exact.Contains(pkg)) return true;
        foreach (var pfx in prefixes)
            if (pkg.StartsWith(pfx)) return true;
        return false;
    }

    public async Task<ScoreResult> ScorePackageAsync(
        AdbService adb,
        string pkg,
        int recentCount10m,
        HashSet<string>? customExact = null,
        List<string>? customPrefixes = null,
        CancellationToken ct = default)
    {
        var score = 0;
        var reasons = new List<string>();
        var details = new Dictionary<string, object?> { ["pkg"] = pkg, ["recent_count_10m"] = recentCount10m };

        if (IsAllowlisted(pkg, customExact, customPrefixes))
            return new ScoreResult(pkg, 0, ["allowlisted"], details);

        // ── 起動頻度 ──────────────────────────────────────────────
        if (recentCount10m >= 8)      { score += 30; reasons.Add($"front-display頻度が高い ({recentCount10m}/10min)"); }
        else if (recentCount10m >= 5) { score += 20; reasons.Add($"front-display頻度がやや高い ({recentCount10m}/10min)"); }
        else if (recentCount10m >= 3) { score += 10; reasons.Add($"front-display頻度シグナル ({recentCount10m}/10min)"); }

        // ── パッケージ名 ──────────────────────────────────────────
        if (SuspiciousNameRe.IsMatch(pkg))
        { score += 5; reasons.Add("パッケージ名に広告/最適化系の弱いシグナル"); }

        // ── ADB情報収集 ───────────────────────────────────────────
        string? installer  = null;
        List<string> paths = [];
        bool? launcherPresent = null;
        string dumpsysTxt  = "";
        string appopsTxt   = "";

        try { installer = await adb.GetInstallerAsync(pkg, ct); }
        catch (Exception ex) { reasons.Add($"installer取得失敗: {ex.Message}"); }

        try { launcherPresent = await adb.QueryLauncherPresenceAsync(pkg, ct); }
        catch (Exception ex) { reasons.Add($"launcher判定失敗: {ex.Message}"); }

        try { dumpsysTxt = await adb.DumpsysPackageAsync(pkg, ct); }
        catch (Exception ex) { reasons.Add($"dumpsys失敗: {ex.Message}"); }

        try { appopsTxt = await adb.AppopsGetAsync(pkg, ct); }
        catch (Exception ex) { reasons.Add($"appops取得失敗: {ex.Message}"); }

        details["installer"]       = installer;
        details["launcher_present"] = launcherPresent;

        // ── インストーラ ──────────────────────────────────────────
        if (installer == null)
        { score += 10; reasons.Add("インストーラ不明"); }
        else if (!TrustedInstallers.Contains(installer))
        { score += 15; reasons.Add($"非標準/未知のインストーラ: {installer}"); }

        // ── 権限シグナル ──────────────────────────────────────────
        var requestedPerms = ParseRequestedPermissions(dumpsysTxt);
        details["requested_permissions"] = requestedPerms;
        foreach (var p in requestedPerms)
        {
            if (PermSignals.TryGetValue(p, out var pts))
            { score += pts; reasons.Add($"権限シグナル: {p} (+{pts})"); }
        }

        if (!string.IsNullOrEmpty(dumpsysTxt) && dumpsysTxt.Contains("AccessibilityService")
            && !requestedPerms.Contains("BIND_ACCESSIBILITY_SERVICE"))
        { score += 15; reasons.Add("AccessibilityService 定義の痕跡 (+15)"); }

        // ── AppOps ───────────────────────────────────────────────
        var appopsFound = new List<string>();
        foreach (var (op, pts) in AppopsSignals)
        {
            if (Regex.IsMatch(appopsTxt, $@"\b{Regex.Escape(op)}\b\s*:\s*allow\b", RegexOptions.IgnoreCase))
            { score += pts; appopsFound.Add(op); reasons.Add($"AppOps許可: {op} (+{pts})"); }
        }
        details["appops_allow_signals"] = appopsFound;

        // ── 挙動テキスト ──────────────────────────────────────────
        var behaviorFound = new Dictionary<string, bool>();
        if (!string.IsNullOrEmpty(dumpsysTxt))
        {
            behaviorFound = ParseBehaviorTextSignals(dumpsysTxt);
            details["behavior_text_signals"] = behaviorFound;
            foreach (var (key, found) in behaviorFound)
            {
                if (found)
                {
                    var (label, pts) = BehaviorTextSignals[key];
                    score += pts;
                    reasons.Add($"挙動シグナル: {label} (+{pts})");
                }
            }
        }

        // ── 複合ルール ────────────────────────────────────────────
        var hasOverlay = requestedPerms.Contains("SYSTEM_ALERT_WINDOW") || appopsFound.Contains("SYSTEM_ALERT_WINDOW");
        var hasA11y    = requestedPerms.Contains("BIND_ACCESSIBILITY_SERVICE")
                         || (!string.IsNullOrEmpty(dumpsysTxt) && dumpsysTxt.Contains("AccessibilityService"));
        var hasUsage   = requestedPerms.Contains("PACKAGE_USAGE_STATS") || appopsFound.Contains("GET_USAGE_STATS");
        var bootRx     = behaviorFound.GetValueOrDefault("android.intent.action.BOOT_COMPLETED");
        var userPresentRx = behaviorFound.GetValueOrDefault("android.intent.action.USER_PRESENT");

        if (launcherPresent == false && recentCount10m >= 3)
        { score += 15; reasons.Add("ランチャー無し/非通常 + 前面表示頻度 (HiddenAds系シグナル)"); }
        else if (launcherPresent == null && recentCount10m >= 5)
        { score += 8; reasons.Add("ランチャー不明 + 前面表示高頻度"); }

        if (hasOverlay && hasA11y)
        { score += 30; reasons.Add("overlay + accessibility の組み合わせ (+30)"); }
        if (hasOverlay && hasUsage)
        { score += 15; reasons.Add("overlay + usage stats の組み合わせ (+15)"); }
        if (userPresentRx && (launcherPresent == false || launcherPresent == null))
        { score += 20; reasons.Add("USER_PRESENT監視 + ランチャー非通常 (HiddenAds疑い)"); }
        if (bootRx && hasOverlay)
        { score += 12; reasons.Add("BOOT_COMPLETED + overlay (再起動後継続広告の疑い)"); }
        if (recentCount10m >= 5 && hasOverlay && installer != null && !TrustedInstallers.Contains(installer))
        { score += 15; reasons.Add("高頻度前面表示 + 非標準インストーラ + overlay"); }

        details["composite_flags"] = new Dictionary<string, bool>
        {
            ["has_overlay_perm"] = hasOverlay, ["has_a11y"] = hasA11y,
            ["has_usage"] = hasUsage, ["boot_receiver"] = bootRx,
            ["user_present_receiver"] = userPresentRx,
        };

        score = Math.Min(score, 180);
        details["score"] = score;

        return new ScoreResult(pkg, score, reasons, details);
    }

    // ── パース ヘルパー ────────────────────────────────────────────
    public List<string> ParseRequestedPermissions(string dumpsysTxt)
    {
        if (string.IsNullOrEmpty(dumpsysTxt)) return [];
        var perms = new HashSet<string>();
        foreach (var line in dumpsysTxt.Split('\n'))
        {
            if (!line.Contains("permission.") && !line.TrimStart().StartsWith("android.permission.")
                && !line.TrimStart().StartsWith("com.android.")) continue;
            foreach (Match m in Regex.Matches(line, @"([A-Z_]{3,}|android\.permission\.[A-Z_]+)"))
            {
                var v = m.Value;
                if (v.StartsWith("android.permission."))
                    perms.Add(v.Split('.').Last());
                else if (v == v.ToUpper())
                    perms.Add(v);
            }
        }
        if (dumpsysTxt.Contains("BIND_ACCESSIBILITY_SERVICE"))
            perms.Add("BIND_ACCESSIBILITY_SERVICE");
        return [.. perms.OrderBy(x => x)];
    }

    public Dictionary<string, bool> ParseBehaviorTextSignals(string dumpsysTxt)
    {
        var result = new Dictionary<string, bool>();
        var low = dumpsysTxt.ToLowerInvariant();
        foreach (var key in BehaviorTextSignals.Keys)
            result[key] = low.Contains(key.ToLowerInvariant());
        return result;
    }
}
