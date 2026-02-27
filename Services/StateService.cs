using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AdwScanGui.Models;

namespace AdwScanGui.Services;

/// <summary>
/// allowlist / state / event log の永続化サービス
/// </summary>
public class StateService
{
    private readonly string _baseDir;
    private AllowlistData _allowlist;
    private StateData     _state;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ── 起動頻度バッファ（メモリ内） ──────────────────────────────
    private readonly ConcurrentDictionary<string, Queue<double>> _eventsByPkg = new();
    private const int MaxQueueLen = 500;

    public StateService()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _baseDir = Path.Combine(home, ".adwscangui");
        Directory.CreateDirectory(_baseDir);
        _allowlist = LoadAllowlist();
        _state     = LoadState();
    }

    // ── Allowlist ─────────────────────────────────────────────────
    private static readonly string[] DefaultPrefixes =
        ["com.android.", "com.google.android.", "com.google.", "android."];
    private static readonly string[] DefaultExact =
    [
        "com.android.systemui", "com.android.settings",
        "com.google.android.gms", "com.google.android.gsf",
        "com.google.android.packageinstaller", "com.android.packageinstaller"
    ];

    private AllowlistData LoadAllowlist()
    {
        var path = Path.Combine(_baseDir, "allowlist.json");
        if (File.Exists(path))
        {
            try
            {
                var data = JsonSerializer.Deserialize<AllowlistData>(File.ReadAllText(path), JsonOpts);
                if (data != null) return data;
            }
            catch { }
        }
        var def = new AllowlistData([.. DefaultExact], [.. DefaultPrefixes]);
        SaveAllowlist(def);
        return def;
    }

    private void SaveAllowlist(AllowlistData data)
    {
        var path = Path.Combine(_baseDir, "allowlist.json");
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
    }

    public HashSet<string> GetAllowlistExact()    => [.. _allowlist.Exact];
    public List<string>    GetAllowlistPrefixes() => [.. _allowlist.Prefixes];

    public void AddAllowlistExact(string pkg)
    {
        _allowlist.Exact.Add(pkg);
        SaveAllowlist(_allowlist);
    }

    public void RemoveAllowlistExact(string pkg)
    {
        _allowlist.Exact.Remove(pkg);
        SaveAllowlist(_allowlist);
    }

    // ── State ─────────────────────────────────────────────────────
    private StateData LoadState()
    {
        var path = Path.Combine(_baseDir, "state.json");
        if (File.Exists(path))
        {
            try
            {
                var data = JsonSerializer.Deserialize<StateData>(File.ReadAllText(path), JsonOpts);
                if (data != null) return data;
            }
            catch { }
        }
        return new StateData([], []);
    }

    public void SaveState()
    {
        var path = Path.Combine(_baseDir, "state.json");
        File.WriteAllText(path, JsonSerializer.Serialize(_state, JsonOpts));
    }

    public Dictionary<string, ActionRecord> GetActioned() => _state.Actioned;
    public Dictionary<string, double>       GetLastSeen() => _state.LastSeen;

    public void RecordAction(string pkg, string action, int score, List<string> reasons)
    {
        _state.Actioned[pkg] = new ActionRecord(
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), action, score, reasons);
        SaveState();
    }

    public bool IsActioned(string pkg) => _state.Actioned.ContainsKey(pkg);

    public void UpdateLastSeen(string pkg, double ts)
    {
        _state.LastSeen[pkg] = ts;
    }

    // ── イベントバッファ ──────────────────────────────────────────
    public void PushEvent(Event evt)
    {
        var q = _eventsByPkg.GetOrAdd(evt.Package, _ => new Queue<double>());
        lock (q)
        {
            q.Enqueue(evt.Timestamp);
            while (q.Count > MaxQueueLen) q.Dequeue();
        }
    }

    public int CountRecent(string pkg, double windowSec = 600)
    {
        if (!_eventsByPkg.TryGetValue(pkg, out var q)) return 0;
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - windowSec;
        lock (q) return q.Count(ts => ts >= cutoff);
    }

    // ── イベントログ（ndjson追記） ────────────────────────────────
    public void AppendEventLog(Event evt)
    {
        var path = Path.Combine(_baseDir, "events.ndjson");
        var line = JsonSerializer.Serialize(new
        {
            ts       = evt.Timestamp,
            time     = evt.TimeFormatted,
            pkg      = evt.Package,
            activity = evt.Activity,
            raw      = evt.Raw,
        }, JsonOpts).Replace(Environment.NewLine, "");
        File.AppendAllText(path, line + "\n");
    }
}

// ── Data classes ─────────────────────────────────────────────────
public class AllowlistData(List<string> exact, List<string> prefixes)
{
    public List<string> Exact    { get; set; } = exact;
    public List<string> Prefixes { get; set; } = prefixes;
}

public class StateData(Dictionary<string, ActionRecord> actioned, Dictionary<string, double> lastSeen)
{
    public Dictionary<string, ActionRecord> Actioned { get; set; } = actioned;
    public Dictionary<string, double>       LastSeen  { get; set; } = lastSeen;
}

public record ActionRecord(string Time, string Action, int Score, List<string> Reasons);
