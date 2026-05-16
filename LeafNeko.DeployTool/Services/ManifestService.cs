using LeafNeko.DeployTool.Models;

namespace LeafNeko.DeployTool.Services;

public class ManifestService
{
    public List<AppItem> Parse(string content)
    {
        var items = new List<AppItem>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split('|');
            if (parts.Length < 3)
                continue;

            items.Add(new AppItem
            {
                Name = parts[0].Trim(),
                Url = parts[1].Trim(),
                Category = parts[2].Trim()
            });
        }

        return items;
    }
}
