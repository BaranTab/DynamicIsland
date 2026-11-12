using System.Globalization;
using System.IO;

namespace DynamicIsland.Services;

public static class Format
{
    public static readonly CultureInfo Tr = new("tr-TR");

    /// <summary>"az önce", "5 dk önce", "3 saat önce", "2 gün önce", "4 Kas"</summary>
    public static string Relative(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff.TotalMinutes < 1) return "az önce";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} dk önce";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} saat önce";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} gün önce";
        return time.ToString("d MMM", Tr);
    }

    /// <summary>C:\Users\baran\projeler\x → ~\projeler\x</summary>
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
