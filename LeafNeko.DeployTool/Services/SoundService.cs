using System.IO;
using System.Media;

namespace LeafNeko.DeployTool.Services;

/// <summary>
/// 自定义音效服务 — 从内存流播放嵌入的 WAV 资源，避免文件依赖。
/// </summary>
public static class SoundService
{
    private static readonly Dictionary<string, byte[]> WavCache = new();

    /// <summary>严肃警告（许可条款等）</summary>
    public static void PlayIcechime() => Play("icechime");

    /// <summary>提醒通知</summary>
    public static void PlayDisable() => Play("disable");

    /// <summary>展开提示（清单加载完成等）</summary>
    public static void PlayBindDone() => Play("bind_done");

    /// <summary>报错</summary>
    public static void PlaySigmaDisable() => Play("sigma_disable");

    /// <summary>完成</summary>
    public static void PlayStart() => Play("start");

    private static void Play(string name)
    {
        try
        {
            if (!WavCache.TryGetValue(name, out var bytes))
            {
                var path = Path.Combine(AppContext.BaseDirectory, "assets", $"{name}.wav");
                if (!File.Exists(path)) return;
                bytes = File.ReadAllBytes(path);
                WavCache[name] = bytes;
            }

            using var ms = new MemoryStream(bytes);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch
        {
            // 音效播放失败不影响主流程
        }
    }
}
