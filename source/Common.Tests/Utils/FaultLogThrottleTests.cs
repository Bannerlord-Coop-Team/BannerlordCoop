using Common.Util;
using System;

namespace Common.Tests.Utils;

public sealed class FaultLogThrottleTests
{
    [Fact]
    public void Classify_FirstOccurrence_IsLoggedInFull()
    {
        var throttle = new FaultLogThrottle();

        Assert.Equal(FaultLogAction.Full, throttle.Classify(new InvalidOperationException("boom"), out long repeats));
        Assert.Equal(0, repeats);
    }

    [Fact]
    public void Classify_SameFaultAgain_IsSuppressedUntilTheIntervalIsReached()
    {
        var throttle = new FaultLogThrottle(repeatInterval: 3);
        var fault = new InvalidOperationException("boom");

        throttle.Classify(fault, out _);

        Assert.Equal(FaultLogAction.Suppress, throttle.Classify(fault, out _));
        Assert.Equal(FaultLogAction.Suppress, throttle.Classify(fault, out _));

        Assert.Equal(FaultLogAction.Summary, throttle.Classify(fault, out long repeats));
        Assert.Equal(3, repeats);
    }

    [Fact]
    public void Classify_ADifferentFault_GetsItsOwnRunWithoutRestartingTheFirst()
    {
        var throttle = new FaultLogThrottle(repeatInterval: 10);
        var first = new InvalidOperationException("first");

        throttle.Classify(first, out _);
        throttle.Classify(first, out _);

        Assert.Equal(FaultLogAction.Full, throttle.Classify(new InvalidOperationException("second"), out long repeats));
        Assert.Equal(0, repeats);

        // The first fault keeps its run rather than being reported in full again.
        Assert.Equal(FaultLogAction.Suppress, throttle.Classify(first, out long firstRepeats));
        Assert.Equal(2, firstRepeats);
    }

    [Fact]
    public void Classify_TheSameFaultFromTwoSources_IsReportedForEach()
    {
        var throttle = new FaultLogThrottle();
        var fault = new NullReferenceException("Object reference not set to an instance of an object.");

        Assert.Equal(FaultLogAction.Full, throttle.Classify("peer 1", fault, out _));

        // Two peers can hit the same null reference, and each has to be named.
        Assert.Equal(FaultLogAction.Full, throttle.Classify("peer 2", fault, out long repeats));
        Assert.Equal(0, repeats);
    }

    [Fact]
    public void Classify_TwoSourcesFailingInAlternation_StaysThrottled()
    {
        var throttle = new FaultLogThrottle(repeatInterval: 100);
        var fault = new InvalidOperationException("every frame");

        throttle.Classify("first", fault, out _);
        throttle.Classify("second", fault, out _);

        for (int tick = 0; tick < 5; tick++)
        {
            Assert.Equal(FaultLogAction.Suppress, throttle.Classify("first", fault, out _));
            Assert.Equal(FaultLogAction.Suppress, throttle.Classify("second", fault, out _));
        }
    }

    [Fact]
    public void Classify_WithoutAnException_StillReportsTheFirstOccurrenceInFull()
    {
        var throttle = new FaultLogThrottle(repeatInterval: 2);

        Assert.Equal(FaultLogAction.Full, throttle.Classify("peer 1", "unhandled payload", out _));
        Assert.Equal(FaultLogAction.Suppress, throttle.Classify("peer 1", "unhandled payload", out _));
        Assert.Equal(FaultLogAction.Summary, throttle.Classify("peer 1", "unhandled payload", out long repeats));
        Assert.Equal(2, repeats);
    }

    [Fact]
    public void Classify_PastCapacity_ForgetsWhatItTrackedAndReportsInFullAgain()
    {
        var throttle = new FaultLogThrottle(repeatInterval: 100, capacity: 2);
        var fault = new InvalidOperationException("boom");

        throttle.Classify("first", fault, out _);
        throttle.Classify("second", fault, out _);
        throttle.Classify("third", fault, out _);

        Assert.Equal(FaultLogAction.Full, throttle.Classify("first", fault, out _));
    }
}
