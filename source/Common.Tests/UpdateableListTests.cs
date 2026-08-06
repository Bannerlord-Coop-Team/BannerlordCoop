using Common.Logging;
using System;
using System.Collections.Generic;

namespace Common.Tests;

/// <summary>
/// Covers the guard in <see cref="UpdateableList.UpdateAll"/>: the engine's tick drives this list, so a
/// faulting entry must not end the process or stop the entries behind it.
/// </summary>
public sealed class UpdateableListTests
{
    [Fact]
    public void UpdateAll_WhenAnEntryThrows_SuppressesItAndStillUpdatesTheRest()
    {
        var updated = new List<string>();
        var list = new UpdateableList();

        // Higher priority runs first, so the faulting entry is ahead of the one that must still run.
        list.Add(new StubUpdateable(priority: 10, onUpdate: () => throw new InvalidOperationException("update failure")));
        list.Add(new StubUpdateable(priority: 0, onUpdate: () => updated.Add("healthy")));

        list.UpdateAll(TimeSpan.Zero);

        Assert.Equal(new[] { "healthy" }, updated);
    }

    [Fact]
    public void UpdateAll_WhenAnEntryKeepsThrowing_StillUpdatesTheRestEveryTick()
    {
        var updated = new List<string>();
        var list = new UpdateableList();

        list.Add(new StubUpdateable(priority: 10, onUpdate: () => throw new InvalidOperationException("update failure")));
        list.Add(new StubUpdateable(priority: 0, onUpdate: () => updated.Add("healthy")));

        // Throttling the repeated log must not throttle the work itself.
        for (int tick = 0; tick < 3; tick++)
        {
            list.UpdateAll(TimeSpan.Zero);
        }

        Assert.Equal(new[] { "healthy", "healthy", "healthy" }, updated);
    }

    [Fact]
    public void UpdateAll_WhenTwoEntriesRaiseTheSameFault_ReportsBothOfThem()
    {
        // One throttle serves the whole list, and both entries have to be named.
        const string marker = "same fault from two updateables";
        var reported = new List<string>();
        void Capture(string line)
        {
            if (!line.Contains(marker)) return;
            lock (reported) reported.Add(line);
        }

        var list = new UpdateableList();
        list.Add(new FaultingFirst(marker));
        list.Add(new FaultingSecond(marker));

        OutputSinkManager.AddLogCallback(Capture);
        try
        {
            list.UpdateAll(TimeSpan.Zero);
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(Capture);
        }

        Assert.Contains(reported, line => line.Contains(nameof(FaultingFirst)));
        Assert.Contains(reported, line => line.Contains(nameof(FaultingSecond)));
    }

    [Fact]
    public void UpdateAll_WithoutFailures_UpdatesEveryEntryInPriorityOrder()
    {
        var updated = new List<string>();
        var list = new UpdateableList();

        list.Add(new StubUpdateable(priority: 0, onUpdate: () => updated.Add("low")));
        list.Add(new StubUpdateable(priority: 10, onUpdate: () => updated.Add("high")));

        list.UpdateAll(TimeSpan.Zero);

        Assert.Equal(new[] { "high", "low" }, updated);
    }

    /// <summary>Two types, because the throttle keys on the entry's type name.</summary>
    private sealed class FaultingFirst : IUpdateable
    {
        private readonly string message;

        public FaultingFirst(string message) => this.message = message;

        public int Priority => 10;

        public void Update(TimeSpan frameTime) => throw new InvalidOperationException(message);
    }

    /// <inheritdoc cref="FaultingFirst"/>
    private sealed class FaultingSecond : IUpdateable
    {
        private readonly string message;

        public FaultingSecond(string message) => this.message = message;

        public int Priority => 0;

        public void Update(TimeSpan frameTime) => throw new InvalidOperationException(message);
    }

    private sealed class StubUpdateable : IUpdateable
    {
        private readonly Action onUpdate;

        public StubUpdateable(int priority, Action onUpdate)
        {
            this.onUpdate = onUpdate;
            Priority = priority;
        }

        public int Priority { get; }

        public void Update(TimeSpan frameTime) => onUpdate();
    }
}
