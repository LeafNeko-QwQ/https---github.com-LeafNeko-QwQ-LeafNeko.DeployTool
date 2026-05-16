using System.Diagnostics;
using System.IO;

namespace LeafNeko.DeployTool.Services;

public class DownloadService
{
    private readonly DeployService _deploy;

    public DownloadService()
    {
        _deploy = new DeployService();
    }

    public async Task InstallAppsAsync(
        List<ViewModels.AppItemViewModel> apps,
        IProgress<string> statusCallback,
        IProgress<double> overallProgress,
        IProgress<(int index, double progress)>? itemProgress = null,
        IProgress<string>? speedCallback = null,
        IProgress<string>? filePathCallback = null,
        CancellationToken ct = default)
    {
        var selected = apps.Where(a => a.IsSelected).ToList();
        var total = selected.Count;
        var completed = 0;

        var sw = Stopwatch.StartNew();

        statusCallback.Report($"开始安装 {total} 个软件...");

        for (var i = 0; i < selected.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var app = selected[i];
            app.Status = Models.AppStatus.Downloading;
            statusCallback.Report($"正在下载: {app.Name}");

            try
            {
                var currentIndex = i;
                await _deploy.DownloadAndInstallAppAsync(app.Name, app.Url,
                    new Progress<double>(p =>
                    {
                        app.DownloadProgress = p;
                        itemProgress?.Report((currentIndex, p));
                    }),
                    speedCallback,
                    filePathCallback);

                app.Status = Models.AppStatus.Installing;
                statusCallback.Report($"正在安装: {app.Name}");

                await Task.Delay(2000, ct);

                app.Status = Models.AppStatus.Completed;
                app.IsSelected = false;
            }
            catch (Exception ex)
            {
                app.Status = Models.AppStatus.Error;
                app.ErrorMessage = ex.Message;
                statusCallback.Report($"出错: {app.Name} - {ex.Message}");
            }

            completed++;
            overallProgress.Report((double)completed / total * 100);

            if (speedCallback != null)
            {
                var elapsed = sw.Elapsed;
                var elapsedText = elapsed.TotalHours >= 1
                    ? $"已用 {elapsed.Hours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
                    : elapsed.TotalMinutes >= 1
                        ? $"已用 {elapsed.Minutes}:{elapsed.Seconds:D2}"
                        : $"已用 {elapsed.Seconds}s";
                speedCallback.Report($"{elapsedText} | 已完成 {completed}/{total}");
            }
        }

        statusCallback.Report("安装完成，正在清理缓存...");
        _deploy.CleanTemp();
        statusCallback.Report("就绪，等待操作");
    }
}
