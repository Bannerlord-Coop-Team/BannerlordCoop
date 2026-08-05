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
    public void Classify_ADifferentFault_ResetsTheRepeatRun()
    {
        var throttle = new FaultLogThrottle(repeatInterval: 2);
        var first = new InvalidOperationException("first");

        throttle.Classify(first, out _);
        throttle.Classify(first, out _);

        // A new fault must be reported immediately rather than inheriting the previous run's position.
        Assert.Equal(FaultLogAction.Full, throttle.Classify(new InvalidOperationException("second"), out long repeats));
        Assert.Equal(0, repeats);

        // ...and the previous fault starts over, so it is reported in full again too.
        Assert.Equal(FaultLogAction.Full, throttle.Classify(first, out _));
    }
}
