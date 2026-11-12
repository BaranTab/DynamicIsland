using System.Diagnostics;
using System.IO;

namespace DynamicIsland.Services;

/// <summary>Dış uygulamaları ve adresleri açar.</summary>
public static class Launcher
{
    public static void OpenUrl(string url) =>
        TryStart(new ProcessStartInfo(url) { UseShellExecute = true });

    public static void OpenSettingsFile()
    {
        SettingsService.Load(); // Yoksa oluştursun
        TryStart(new ProcessStartInfo("notepad.exe", $"\"{SettingsService.FilePath}\"") { UseShellExecute = true });
    }

    /// <summary>Klasörde yeni bir terminal açıp "claude" komutunu çalıştırır.</summary>
    public static void OpenClaudeCode(string path)
    {
        if (!Directory.Exists(path)) return;
        var dir = path.Length > 3 ? path.TrimEnd('\\') : path;

        try
        {
            // Windows Terminal varsa onu kullan
            Process.Start(new ProcessStartInfo("wt.exe", $"-d \"{dir}\" cmd /k claude") { UseShellExecute = true });
        }
        catch
        {
            TryStart(new ProcessStartInfo("cmd.exe", "/k claude") { WorkingDirectory = dir, UseShellExecute = true });
        }
    }

    /// <summary>VS Code'u açar; path verilirse o klasörü/dosyayı açar.</summary>
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
            Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(programFiles, "Microsoft VS Code", "Code.exe")
        };

        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }

    private static void TryStart(ProcessStartInfo info)
    {
        try { Process.Start(info); }
        catch { /* Uygulama bulunamadı */ }
    }
}
