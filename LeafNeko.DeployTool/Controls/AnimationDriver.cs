using System.Collections.Generic;
using System.Windows.Media;

namespace LeafNeko.DeployTool.Controls;

/// <summary>
/// 全局单帧循环驱动 — 所有 AppCard 共用同一个 CompositionTarget.Rendering 事件，
/// 避免每张卡片单独注册导致的事件分发开销（N→1）。
/// </summary>
public static class AnimationDriver
{
    private static readonly List<AppCard> Cards = new();
    private static bool _isRunning;

    public static void Register(AppCard card)
    {
        Cards.Add(card);
        if (!_isRunning)
        {
            _isRunning = true;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    public static void Unregister(AppCard card)
    {
        Cards.Remove(card);
        if (Cards.Count == 0 && _isRunning)
        {
            _isRunning = false;
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private static void OnFrame(object? sender, EventArgs e)
    {
        // snapshot to allow unregister during Tick
        var count = Cards.Count;
        for (var i = count - 1; i >= 0; i--)
        {
            if (i < Cards.Count)
                Cards[i].Tick();
        }
    }
}
