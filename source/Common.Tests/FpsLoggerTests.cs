using Common;
using System;
using System.Collections.Generic;
using Xunit;

namespace Common.Tests;

public class FpsLoggerTests : IDisposable
{
    public void Dispose()
    {
        FpsLogger.Enabled = false;
    }

    [Fact]
    public void Update_WhenDisabled_DoesNotReport()
    {
        FpsLogger.Enabled = false;
        var reports = new List<FpsWindowStats>();
        var logger = new FpsLogger(TimeSpan.FromMilliseconds(100), reports.Add);

        for (int i = 0; i < 20; i++)
        {
            logger.Update(TimeSpan.FromMilliseconds(16));
        }

        Assert.Empty(reports);
    }

    [Fact]
    public void Update_WhenEnabledButWindowNotElapsed_DoesNotReport()
    {
        FpsLogger.Enabled = true;
        var reports = new List<FpsWindowStats>();
        var logger = new FpsLogger(TimeSpan.FromMilliseconds(100), reports.Add);

        logger.Update(TimeSpan.FromMilliseconds(16));
        logger.Update(TimeSpan.FromMilliseconds(16));

        Assert.Empty(reports);
    }

    [Fact]
    public void Update_WhenWindowElapsed_ReportsAverageMinimumAndMaximumFps()
    {
        FpsLogger.Enabled = true;
        var reports = new List<FpsWindowStats>();
        var logger = new FpsLogger(TimeSpan.FromMilliseconds(100), reports.Add);

        logger.Update(TimeSpan.FromMilliseconds(10));
        logger.Update(TimeSpan.FromMilliseconds(20));
        logger.Update(TimeSpan.FromMilliseconds(30));
        logger.Update(TimeSpan.FromMilliseconds(40));

        var report = Assert.Single(reports);
        Assert.Equal(4, report.Frames);
        Assert.Equal(0.1, report.Seconds, 3);
        Assert.Equal(40, report.AverageFps, 1);
        Assert.Equal(25, report.MinimumFps, 1);
        Assert.Equal(100, report.MaximumFps, 1);
    }

    [Fact]
    public void Update_WithZeroFrameTime_ExcludesFrameFromStats()
    {
        FpsLogger.Enabled = true;
        var reports = new List<FpsWindowStats>();
        var logger = new FpsLogger(TimeSpan.FromMilliseconds(50), reports.Add);

        logger.Update(TimeSpan.Zero);
        logger.Update(TimeSpan.FromMilliseconds(50));

        var report = Assert.Single(reports);
        Assert.Equal(1, report.Frames);
        Assert.Equal(20, report.MinimumFps, 1);
        Assert.Equal(20, report.MaximumFps, 1);
        Assert.Equal(20, report.AverageFps, 1);
    }

    [Fact]
    public void Update_AfterReporting_StartsNewWindow()
    {
        FpsLogger.Enabled = true;
        var reports = new List<FpsWindowStats>();
        var logger = new FpsLogger(TimeSpan.FromMilliseconds(50), reports.Add);

        logger.Update(TimeSpan.FromMilliseconds(50));
        logger.Update(TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, reports.Count);
        Assert.Equal(1, reports[1].Frames);
        Assert.Equal(20, reports[1].AverageFps, 1);
    }
}
