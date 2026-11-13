using System.IO;
using DynamicIsland.Localization;

namespace DynamicIsland.Services;

public static class Format
{
    /// <summary>"just now", "5 min ago", "3 h ago", "2 d ago", "4 Nov" (in the current UI language)</summary>
    public static string Relative(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff.TotalMinutes < 1) return Loc.T("TimeJustNow");
        if (diff.TotalMinutes < 60) return Loc.T("TimeMinutesAgo", (int)diff.TotalMinutes);
        if (diff.TotalHours < 24) return Loc.T("TimeHoursAgo", (int)diff.TotalHours);
        if (diff.TotalDays < 7) return Loc.T("TimeDaysAgo", (int)diff.TotalDays);
        return time.ToString("d MMM", Loc.Instance.Culture);
    }

    /// <summary>C:\Users\baran\projects\x → ~\projects\x</summary>
    public static string ShortPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.StartsWith(home, StringComparison.OrdinalIgnoreCase) ? "~" + path[home.Length..] : path;
    }

    public static string FolderName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
