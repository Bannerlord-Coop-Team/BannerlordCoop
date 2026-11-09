using System;
using System.Diagnostics;

namespace Common.BattleBenchmark;

/// <summary>Reads this process's garbage collection, allocation, CPU and memory counters for the battle benchmark.</summary>
public interface IBattleBenchmarkProcessSampler
{
    /// <summary>Turns on per-process allocation counting, which cannot be turned off again.</summary>
    void EnableAllocationMonitoring();

    BattleBenchmarkProcessSample Sample();
}

/// <inheritdoc cref="IBattleBenchmarkProcessSampler"/>
public class BattleBenchmarkProcessSampler : IBattleBenchmarkProcessSampler
{
    public void EnableAllocationMonitoring()
    {
        // .NET Framework counts allocations only once monitoring is on; newer runtimes always count them.
        if (!AppDomain.MonitoringIsEnabled)
            AppDomain.MonitoringIsEnabled = true;
    }

    public BattleBenchmarkProcessSample Sample()
    {
        using var process = Process.GetCurrentProcess();
        return new BattleBenchmarkProcessSample(
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            AppDomain.MonitoringIsEnabled ? AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize : -1,
            GC.GetTotalMemory(false),
            process.TotalProcessorTime,
            process.WorkingSet64,
            process.PeakWorkingSet64,
            process.PrivateMemorySize64);
    }
}

/// <summary>One reading of the counters <see cref="IBattleBenchmarkProcessSampler"/> collects.</summary>
public readonly struct BattleBenchmarkProcessSample
{
    public int Gen0Collections { get; }
    public int Gen1Collections { get; }
    public int Gen2Collections { get; }

    /// <summary>Bytes allocated by the process so far, or -1 when allocation monitoring is off.</summary>
    public long AllocatedBytes { get; }

    public long ManagedHeapBytes { get; }
    public TimeSpan ProcessorTime { get; }
    public long WorkingSetBytes { get; }
    public long PeakWorkingSetBytes { get; }
    public long PrivateBytes { get; }

    public BattleBenchmarkProcessSample(
        int gen0Collections,
        int gen1Collections,
        int gen2Collections,
        long allocatedBytes,
        long managedHeapBytes,
        TimeSpan processorTime,
        long workingSetBytes,
        long peakWorkingSetBytes,
        long privateBytes)
    {
        Gen0Collections = gen0Collections;
        Gen1Collections = gen1Collections;
        Gen2Collections = gen2Collections;
        AllocatedBytes = allocatedBytes;
        ManagedHeapBytes = managedHeapBytes;
        ProcessorTime = processorTime;
        WorkingSetBytes = workingSetBytes;
        PeakWorkingSetBytes = peakWorkingSetBytes;
        PrivateBytes = privateBytes;
    }
}
