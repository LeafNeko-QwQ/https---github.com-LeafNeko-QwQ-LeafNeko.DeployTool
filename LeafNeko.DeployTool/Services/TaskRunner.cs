using System.Collections.ObjectModel;
using System.Diagnostics;
using LeafNeko.DeployTool.Models;

namespace LeafNeko.DeployTool.Services;

public class TaskRunner
{
    private readonly SemaphoreSlim _semaphore;
    private readonly CancellationTokenSource _cts = new();

    public ObservableCollection<DeployTask> Tasks { get; } = new();

    public TaskRunner(int maxConcurrency = 3)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    /// <summary>
    /// 并发执行一组任务。每个 action 接收自己的 DeployTask 用于汇报进度。
    /// </summary>
    public async Task RunAllAsync(
        IEnumerable<(string name, Func<DeployTask, Task> action)> workItems)
    {
        var tasks = new List<Task>();
        foreach (var (name, action) in workItems)
        {
            var deployTask = new DeployTask { Name = name, Status = DeployTaskStatus.Pending };
            Tasks.Add(deployTask);
            tasks.Add(RunOneAsync(deployTask, action));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TaskRunner] 批量任务异常: {ex.Message}");
        }
    }

    private async Task RunOneAsync(DeployTask task, Func<DeployTask, Task> action)
    {
        await _semaphore.WaitAsync(_cts.Token);
        try
        {
            task.Status = DeployTaskStatus.Running;
            Trace.WriteLine($"[TaskRunner] 开始: {task.Name}");
            await action(task);
            task.Status = DeployTaskStatus.Completed;
            Trace.WriteLine($"[TaskRunner] 完成: {task.Name}");
        }
        catch (Exception ex)
        {
            task.Status = DeployTaskStatus.Error;
            task.PhaseText = ex.Message;
            Trace.WriteLine($"[TaskRunner] 出错 {task.Name}: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void CancelAll()
    {
        _cts.Cancel();
        Trace.WriteLine("[TaskRunner] 已请求取消所有任务");
    }

    public void ClearCompleted()
    {
        for (int i = Tasks.Count - 1; i >= 0; i--)
        {
            if (Tasks[i].Status is DeployTaskStatus.Completed or DeployTaskStatus.Error)
                Tasks.RemoveAt(i);
        }
    }
}
