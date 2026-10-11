using System.Diagnostics;
using System.Text.Json;

namespace RlRemapper;

// Uses HidHide's supported CLI, never writes driver registry keys directly.
internal static class HidHide
{
    public sealed record Device(string Name, string Path)
    {
        public override string ToString() => $"{Name}  [{Path}]";
    }
    static string CliPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var other = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        // The installer copies the resolved location into the protected app directory.
        var hint = Path.Combine(AppContext.BaseDirectory, "hidhide-cli.path");
        if (File.Exists(hint))
        {
            var path = File.ReadAllText(hint).Trim();
            if (File.Exists(path)) return path;
        }
        foreach (var dir in new[] { root, other }.Distinct())
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var vendor in Directory.EnumerateDirectories(dir, "*Nefarius*"))
            {
                var folder = Path.Combine(vendor, "HidHide");
                foreach (var path in new[] { Path.Combine(folder, "x64", "HidHideCLI.exe"), Path.Combine(folder, "HidHideCLI.exe") })
                    if (File.Exists(path)) return path;
            }
        }
        throw new InvalidOperationException("HidHide CLI missing. Rerun the installer (restart first if Windows requested a reboot).");
    }
    static string Run(params string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(CliPath()) {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        }};
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        // CLI responses are small; timeout prevents an unresponsive driver from hanging the UI forever.
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10000)) { process.Kill(); throw new TimeoutException("HidHide CLI timed out."); }
        if (process.ExitCode != 0) throw new InvalidOperationException($"HidHide {args[0]} failed: {error}");
        return output;
    }
    public static List<Device> GamingDevices() => ParseGamingDevices(Run("--dev-gaming"));
    internal static List<Device> ParseGamingDevices(string json)
    {
        using var document = JsonDocument.Parse(json);
        var devices = new List<Device>();
        foreach (var group in document.RootElement.EnumerateArray())
        {
            string name = group.GetProperty("friendlyName").GetString() ?? "Controller";
            foreach (var entry in group.GetProperty("devices").EnumerateArray())
            {
                if (!entry.GetProperty("present").GetBoolean() ||
                    !entry.TryGetProperty("gamingDevice", out var gaming) || !gaming.GetBoolean()) continue;
                string? path = entry.GetProperty("deviceInstancePath").GetString();
                if (string.IsNullOrWhiteSpace(path)) continue;
                // Same virtual VID/PID filter as the SDL input side; never offer our virtual controller.
                if (path.Contains("VID_045E&PID_028E", StringComparison.OrdinalIgnoreCase)) continue;
                devices.Add(new Device(name, path));
            }
        }
        return devices.DistinctBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }
    public static void Configure(string path)
    {
        if (!GamingDevices().Any(d => d.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Selected device is no longer connected. Select it again.");
        string exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "RlRemapper.exe"));
        if (!Run("--app-list").Split('\n').Any(line => line.Trim().Equals($"--app-reg \"{exe}\"", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Remapper is not whitelisted in HidHide. Rerun setup before hiding any device.");
        foreach (string hidePath in ControllerDeviceTree.HidePaths(path))
        {
            if (!Run("--dev-list").Split('\n').Any(line => line.Trim().Equals($"--dev-hide \"{hidePath}\"", StringComparison.OrdinalIgnoreCase)))
                Run("--dev-hide", hidePath);
            if (!Run("--dev-list").Split('\n').Any(line => line.Trim().Equals($"--dev-hide \"{hidePath}\"", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("HidHide did not register the selected gamepad interface.");
        }
        Run("--cloak-on");
        if (!Run("--cloak-state").Contains("--cloak-on", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("HidHide did not enable device hiding.");
    }
    public static void PrepareDisconnected(string path)
    {
        string exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "RlRemapper.exe"));
        if (!Run("--app-list").Split('\n').Any(line => line.Trim().Equals($"--app-reg \"{exe}\"", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Remapper is not allowlisted. Rerun setup.");
        if (string.IsNullOrWhiteSpace(path) || !Run("--dev-list").Split('\n').Any(line => line.Trim().Equals($"--dev-hide \"{path}\"", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("First connect the controller and use Start once to configure hiding. Then Stop, unplug it and use Prepare Player 1.");
        foreach (string hidePath in ControllerDeviceTree.HidePaths(path))
            if (!Run("--dev-list").Split('\n').Any(line => line.Trim().Equals($"--dev-hide \"{hidePath}\"", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Controller hiding is incomplete. Connect it and use Start first.");
        Run("--cloak-on");
    }
    public static void ConfigureReconnected(string savedPath)
    {
        var savedPaths = ControllerDeviceTree.HidePaths(savedPath);
        var matches = GamingDevices().Where(device => ControllerDeviceTree.HidePaths(device.Path)
            .Intersect(savedPaths, StringComparer.OrdinalIgnoreCase).Any()).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Reconnect the previously selected controller, then Stop and Start again if necessary.");
        Configure(matches[0].Path);
    }
    public static void Unhide(string path)
    {
        // Never disable the global cloak: other applications may use HidHide too.
        var paths = ControllerDeviceTree.HidePaths(path).ToList();
        foreach (var device in GamingDevices())
        {
            var currentPaths = ControllerDeviceTree.HidePaths(device.Path);
            if (currentPaths.Intersect(paths, StringComparer.OrdinalIgnoreCase).Any()) paths.AddRange(currentPaths);
        }
        foreach (string hidePath in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            if (Run("--dev-list").Split('\n').Any(line => line.Trim().Equals($"--dev-hide \"{hidePath}\"", StringComparison.OrdinalIgnoreCase)))
                Run("--dev-unhide", hidePath);
    }
}
