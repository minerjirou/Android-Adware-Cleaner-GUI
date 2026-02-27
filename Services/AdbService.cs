using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AdwScanGui.Models;

namespace AdwScanGui.Services;

/// <summary>
/// ADBコマンドラッパー。同梱バイナリを一時ディレクトリに展開して使用する。
/// </summary>
public class AdbService : IDisposable
{
    private string _adbPath;
    private string? _serial;
    private bool _disposed;

    private static readonly Regex DisplayedRe = new(
        @"Displayed\s+([A-Za-z0-9._]+)/([A-Za-z0-9.$_/\-]+)",
        RegexOptions.Compiled);

    public string? Serial
    {
        get => _serial;
        set => _serial = value;
    }

    public AdbService(string? serial = null)
    {
        _serial = serial;
        _adbPath = ExtractAdb();
    }

    // ── ADB バイナリの展開 ──────────────────────────────────────────
    private static string ExtractAdb()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "adwscangui");
        Directory.CreateDirectory(tmpDir);

        string resourceName;
        string adbFileName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            resourceName = "AdwScanGui.NativeTools.win_x64.adb.exe";
            adbFileName  = "adb.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx_arm64" : "osx_x64";
            resourceName = $"AdwScanGui.NativeTools.{arch}.adb";
            adbFileName  = "adb";
        }
        else
        {
            resourceName = "AdwScanGui.NativeTools.linux_x64.adb";
            adbFileName  = "adb";
        }

        var destPath = Path.Combine(tmpDir, adbFileName);

        // 埋め込みリソースから展開（初回のみ）
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var fs = File.Create(destPath);
            stream.CopyTo(fs);
        }

        // macOS / Linux: 実行ビット付与
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(destPath))
        {
            try
            {
                var chmod = Process.Start(new ProcessStartInfo("chmod", $"+x \"{destPath}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                chmod?.WaitForExit(3000);
            }
            catch { /* 権限付与失敗は後続で検出される */ }
        }

        // 埋め込みリソースがない場合はPATHのadbを使う
        if (!File.Exists(destPath))
            return "adb";

        return destPath;
    }

    // ── コマンド実行 ──────────────────────────────────────────────
    private List<string> BaseArgs()
    {
        var args = new List<string>();
        if (!string.IsNullOrEmpty(_serial))
        {
            args.Add("-s");
            args.Add(_serial);
        }
        return args;
    }

    public async Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(
        IEnumerable<string> args,
        int timeoutMs = 20_000,
        CancellationToken ct = default)
    {
        var allArgs = BaseArgs().Concat(args);
        var psi = new ProcessStartInfo(_adbPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding  = System.Text.Encoding.UTF8,
        };
        foreach (var a in allArgs)
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var stdOut = await proc.StandardOutput.ReadToEndAsync(cts.Token);
        var stdErr = await proc.StandardError.ReadToEndAsync(cts.Token);
        await proc.WaitForExitAsync(cts.Token);

        return (stdOut, stdErr, proc.ExitCode);
    }

    public async Task<(string Stdout, string Stderr, int ExitCode)> ShellAsync(
        IEnumerable<string> args,
        int timeoutMs = 20_000,
        CancellationToken ct = default)
        => await RunAsync(new[] { "shell" }.Concat(args), timeoutMs, ct);

    // ── デバイス確認 ──────────────────────────────────────────────
    public async Task<string> GetDeviceStateAsync()
    {
        var (stdout, stderr, _) = await RunAsync(new[] { "get-state" }, 10_000);
        return (stdout + stderr).Trim();
    }

    public async Task<bool> IsDeviceReadyAsync()
    {
        try
        {
            var state = await GetDeviceStateAsync();
            return state.Contains("device");
        }
        catch { return false; }
    }

    public async Task<List<string>> GetDeviceListAsync()
    {
        var (stdout, _, _) = await RunAsync(new[] { "devices" }, 10_000);
        var devices = new List<string>();
        foreach (var line in stdout.Split('\n').Skip(1))
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length == 2 && parts[1].Trim() == "device")
                devices.Add(parts[0].Trim());
        }
        return devices;
    }

    // ── logcat ストリーム（非同期） ──────────────────────────────────
    public async IAsyncEnumerable<Models.Event> StreamDisplayedEventsAsync(
        bool clear = false,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (clear)
            await RunAsync(new[] { "logcat", "-c" }, 10_000, ct);

        var args = BaseArgs().Concat(new[] { "logcat", "-s", "ActivityTaskManager", "ActivityManager" }).ToList();
        var psi = new ProcessStartInfo(_adbPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        ct.Register(() =>
        {
            try { proc.Kill(); } catch { }
        });

        string? line;
        while ((line = await proc.StandardOutput.ReadLineAsync(ct)) != null)
        {
            var evt = ParseDisplayedLine(line);
            if (evt != null) yield return evt;
        }
    }

    private static Models.Event? ParseDisplayedLine(string line)
    {
        var m = DisplayedRe.Match(line);
        if (!m.Success) return null;
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        return new Models.Event(ts, m.Groups[1].Value, m.Groups[2].Value, line.TrimEnd());
    }

    // ── ADB Shell ヘルパー ───────────────────────────────────────
    public async Task<List<string>> ListUserPackagesAsync(CancellationToken ct = default)
    {
        var (stdout, _, _) = await ShellAsync(new[] { "pm", "list", "packages", "-3" }, 30_000, ct);
        return stdout.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("package:"))
            .Select(l => l.Substring("package:".Length).Trim())
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<string> DumpsysPackageAsync(string pkg, CancellationToken ct = default)
    {
        var (out1, out2, _) = await ShellAsync(new[] { "dumpsys", "package", pkg }, 40_000, ct);
        return out1 + (string.IsNullOrEmpty(out2) ? "" : "\n" + out2);
    }

    public async Task<string> AppopsGetAsync(string pkg, CancellationToken ct = default)
    {
        var (out1, out2, _) = await ShellAsync(new[] { "cmd", "appops", "get", pkg }, 30_000, ct);
        return out1 + (string.IsNullOrEmpty(out2) ? "" : "\n" + out2);
    }

    public async Task<string?> GetInstallerAsync(string pkg, CancellationToken ct = default)
    {
        var (stdout, stderr, _) = await ShellAsync(new[] { "pm", "list", "packages", "-i", pkg }, 20_000, ct);
        var txt = stdout + "\n" + stderr;
        var m = Regex.Match(txt, $@"package:{Regex.Escape(pkg)}(?:\s+installer=(\S+))?");
        if (m.Success && m.Groups[1].Success)
            return m.Groups[1].Value;

        var ds = await DumpsysPackageAsync(pkg, ct);
        var m2 = Regex.Match(ds, @"installerPackageName=(\S+)");
        return m2.Success ? m2.Groups[1].Value : null;
    }

    public async Task<bool?> QueryLauncherPresenceAsync(string pkg, CancellationToken ct = default)
    {
        try
        {
            var (stdout, stderr, code) = await ShellAsync(
                new[] { "cmd", "package", "resolve-activity", "--brief", pkg }, 15_000, ct);
            if (code == 0 && (stdout + stderr).Contains(pkg))
                return true;
        }
        catch { }

        try
        {
            var ds = await DumpsysPackageAsync(pkg, ct);
            if (ds.Contains("android.intent.action.MAIN") && ds.Contains("android.intent.category.LAUNCHER"))
                return false;
        }
        catch { }

        return null;
    }

    // ── アクション ───────────────────────────────────────────────
    public async Task ForceStopAsync(string pkg, CancellationToken ct = default)
        => await ShellAsync(new[] { "am", "force-stop", pkg }, 20_000, ct);

    public async Task DisableUserAsync(string pkg, CancellationToken ct = default)
        => await ShellAsync(new[] { "pm", "disable-user", "--user", "0", pkg }, 20_000, ct);

    public async Task UninstallUser0Async(string pkg, CancellationToken ct = default)
        => await ShellAsync(new[] { "pm", "uninstall", "--user", "0", pkg }, 30_000, ct);

    public async Task EnableUser0Async(string pkg, CancellationToken ct = default)
        => await ShellAsync(new[] { "pm", "enable", "--user", "0", pkg }, 20_000, ct);

    public async Task InstallExistingAsync(string pkg, CancellationToken ct = default)
        => await ShellAsync(new[] { "cmd", "package", "install-existing", pkg }, 30_000, ct);

    public async Task KillServerAsync()
        => await RunAsync(new[] { "kill-server" }, 10_000);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // 一時展開したadbは他プロセスが使う可能性があるため削除しない
    }
}
