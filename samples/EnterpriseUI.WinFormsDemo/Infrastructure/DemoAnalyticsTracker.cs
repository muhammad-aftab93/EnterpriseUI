using System;
using System.Diagnostics;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

public sealed class DemoAnalyticsTracker
{
    private static readonly Lazy<DemoAnalyticsTracker> _instance = new(() => new DemoAnalyticsTracker());
    public static DemoAnalyticsTracker Instance => _instance.Value;

    private readonly Stopwatch _stopwatch = new();
    private long _startMemory;

    public long LastRenderTimeMs { get; private set; }
    public long LastAllocatedBytes { get; private set; }
    public int TotalDemosViewed { get; private set; }
    public string ActiveDemoId { get; private set; } = string.Empty;
    public long LastManagedMemoryBytes { get; private set; }

    public event Action? MetricsUpdated;

    public void BeginTrack(string demoId)
    {
        ActiveDemoId = demoId;
        TotalDemosViewed++;
        _startMemory = GC.GetTotalMemory(false);
        _stopwatch.Restart();
    }

    public void EndTrack()
    {
        _stopwatch.Stop();
        LastRenderTimeMs = Math.Max(1, _stopwatch.ElapsedMilliseconds);
        var currentMem = GC.GetTotalMemory(false);
        LastAllocatedBytes = Math.Max(0, currentMem - _startMemory);
        MetricsUpdated?.Invoke();
    }

    public string GetFormattedMemory()
    {
        LastManagedMemoryBytes = GC.GetTotalMemory(false);
        return $"{LastManagedMemoryBytes / (1024.0 * 1024.0):F2} MB";
    }
}

