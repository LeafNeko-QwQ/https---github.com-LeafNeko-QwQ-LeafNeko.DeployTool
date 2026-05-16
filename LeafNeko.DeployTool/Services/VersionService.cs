using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LeafNeko.DeployTool.Services;

public partial class VersionService
{
    public record VersionInfo(bool IsInstalled, string? Version, string? DisplayName);

    public VersionInfo? Detect(string appName)
    {
        var paths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var basePath in paths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(basePath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    var displayName = subKey?.GetValue("DisplayName") as string;
                    if (displayName != null && displayName.Contains(appName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new VersionInfo(
                            true,
                            subKey?.GetValue("DisplayVersion") as string,
                            displayName
                        );
                    }
                }
            }
            catch { }
        }

        return null;
    }

    public static bool IsOutdated(string? localVersion, string? downloadUrl)
    {
        if (string.IsNullOrEmpty(localVersion) || string.IsNullOrEmpty(downloadUrl))
            return false;

        var local = ParseVersion(localVersion);
        var remote = ExtractVersionFromUrl(downloadUrl);

        return remote != null && remote > local;
    }

    private static Version? ParseVersion(string v)
    {
        // Try X.Y.Z.W or X.Y.Z or X.Y
        var match = VersionRegex().Match(v);
        if (match.Success)
        {
            try { return new Version(match.Value); }
            catch { }
        }
        return null;
    }

    public static Version? ExtractVersionFromUrl(string url)
    {
        var fileName = Path.GetFileNameWithoutExtension(url);
        // Replace common separators with dots
        var cleaned = fileName
            .Replace("-", ".")
            .Replace("_", ".")
            .Replace(" ", ".");

        // Find version patterns like 24.08, 8.7.7, etc.
        var matches = UrlVersionRegex().Matches(cleaned);
        foreach (Match m in matches)
        {
            var ver = m.Groups[1].Value;
            try
            {
                var parts = ver.Split('.');
                if (parts.Length >= 2)
                {
                    // Only return if it looks like a real version (not just a year)
                    var major = int.Parse(parts[0]);
                    if (major > 0 && major < 999)
                        return new Version(ver);
                }
            }
            catch { }
        }

        return null;
    }

    [GeneratedRegex(@"^\d+\.\d+(\.\d+)?(\.\d+)?")]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"\b(\d+\.\d+(?:\.\d+)?(?:\.\d+)?)\b")]
    private static partial Regex UrlVersionRegex();
}
