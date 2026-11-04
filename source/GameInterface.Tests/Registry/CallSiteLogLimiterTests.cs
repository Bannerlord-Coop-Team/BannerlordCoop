using GameInterface.Registry.Auto;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Registry;

public class CallSiteLogLimiterTests
{
    [Fact]
    public void ShouldLogFull_LogsEachSiteOnceAndSumsRepeatsPerInterval()
    {
        var now = new DateTime(2026, 1, 1);
        var limiter = new CallSiteLogLimiter(8, TimeSpan.FromSeconds(10), () => now);

        Assert.True(limiter.ShouldLogFull("trade", out var count));
        Assert.Equal(0, count);
        Assert.True(limiter.ShouldLogFull("inventory", out _));

        for (int i = 0; i < 1100; i++)
        {
            Assert.False(limiter.ShouldLogFull(i % 2 == 0 ? "trade" : "inventory", out count));
            Assert.Equal(0, count);
        }

        now = now.AddSeconds(10);
        Assert.False(limiter.ShouldLogFull("trade", out count));
        Assert.Equal(1101, count);

        Assert.False(limiter.ShouldLogFull("trade", out count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void ShouldLogFull_SteadyHotCallerGivesOneErrorAndASummaryPerInterval()
    {
        // ticket 4200 had one caller hitting a client guard about 36 times a second for close to an hour
        var now = new DateTime(2026, 1, 1);
        var limiter = new CallSiteLogLimiter(64, TimeSpan.FromSeconds(10), () => now);
        int full = 0, summaries = 0, summed = 0;

        for (int second = 0; second < 60; second++)
        {
            for (int i = 0; i < 36; i++)
            {
                if (limiter.ShouldLogFull("garrison", out var count)) full++;
                if (count > 0) { summaries++; summed += count; }
            }
            now = now.AddSeconds(1);
        }

        Assert.Equal(1, full);
        Assert.Equal(5, summaries);
        Assert.Equal(36 * 50, summed);
    }

    [Fact]
    public void ShouldLogFull_StopsLoggingNewSitesPastTheCap()
    {
        var limiter = new CallSiteLogLimiter(1, TimeSpan.FromSeconds(10), () => DateTime.MinValue);

        Assert.True(limiter.ShouldLogFull("first", out _));
        Assert.False(limiter.ShouldLogFull("second", out _));
    }

    [Fact]
    public void GetCallSite_DiffersByCaller()
    {
        Assert.NotEqual(SiteFromA(), SiteFromB());
        Assert.Equal(SiteFromA(), SiteFromA());
        Assert.Contains(nameof(SiteFromA), SiteFromA());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string SiteFromA() => CallSiteLogLimiter.GetCallSite(0, 2);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string SiteFromB() => CallSiteLogLimiter.GetCallSite(0, 2);
}
