using System.Diagnostics;
using System.IO;

namespace DynamicIsland.Services;

/// <summary>Opens external apps and web addresses.</summary>
public static class Launcher
{
    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // "www.gmail.com" without a scheme would otherwise fail to open
        if (!url.Contains("://")) url = "https://" + url;

        TryStart(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static void OpenSettingsFile()
    {
        SettingsService.Load(); // Creates the file if it doesn't exist yet
        TryStart(new ProcessStartInfo("notepad.exe", $"\"{SettingsService.FilePath}\"") { UseShellExecute = true });
    }

    /// <summary>Opens a new terminal in the folder and runs "claude" there.</summary>
    public static void OpenClaudeCode(string path)
    {
        if (!Directory.Exists(path)) return;
        var dir = path.Length > 3 ? path.TrimEnd('\\') : path;

        try
        {
            // Prefer Windows Terminal when it's installed
            Process.Start(new ProcessStartInfo("wt.exe", $"-d \"{dir}\" cmd /k claude") { UseShellExecute = true });
        }
        catch
        {
            TryStart(new ProcessStartInfo("cmd.exe", "/k claude") { WorkingDirectory = dir, UseShellExecute = true });
        }
    }

    /// <summary>Opens VS Code, optionally with a folder or file.</summary>
    public static void OpenInVsCode(string? path, string? codePath = null, bool newWindow = false)
    {
        var args = new List<string>();
        if (newWindow) args.Add("--new-window");
        if (!string.IsNullOrWhiteSpace(path)) args.Add($"\"{path}\"");
        var argText = string.Join(' ', args);

        var exe = FindVsCode(codePath);
        if (exe != null)
            TryStart(new ProcessStartInfo(exe, argText) { UseShellExecute = false });
        else
            TryStart(new ProcessStartInfo("cmd.exe", $"/c code {argText}") { UseShellExecute = false, CreateNoWindow = true });
    }

    private static string? FindVsCode(string? configured)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        string?[] candidates =
        {
            configured,
            Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"), // user install (most common)
            Path.Combine(programFiles, "Microsoft VS Code", "Code.exe")              // system install
        };

        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }

    private static void TryStart(ProcessStartInfo info)
    {
        try { Process.Start(info); }
        catch { /* App not found */ }
    }
}
