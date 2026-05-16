using Microsoft.Win32;

namespace LeafNeko.DeployTool.Services;

public class VersionService
{
    public record VersionInfo(bool IsInstalled, string? Version, string? DisplayName);

    public VersionInfo? Detect(string appName)
    {
        var paths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
        };

        foreach (var basePath in paths.Take(2))
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
}
