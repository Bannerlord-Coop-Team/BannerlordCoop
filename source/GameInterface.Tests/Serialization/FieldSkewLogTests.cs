using GameInterface.Serialization;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class FieldSkewLogTests
{
    private sealed class SkewedA { }
    private sealed class SkewedB { }

    [Fact]
    public void FirstTime_IsTrueOncePerTypeAndField()
    {
        Assert.True(FieldSkewLog.FirstTime(typeof(SkewedA), "_syncRoot"));
        Assert.False(FieldSkewLog.FirstTime(typeof(SkewedA), "_syncRoot"));
        Assert.False(FieldSkewLog.FirstTime(typeof(SkewedA), "_syncRoot"));

        Assert.True(FieldSkewLog.FirstTime(typeof(SkewedA), "_version"));
        Assert.True(FieldSkewLog.FirstTime(typeof(SkewedB), "_syncRoot"));
    }

    [Fact]
    public void FirstTime_ReportsABurstOnlyOnce()
    {
        int reported = 0;
        for (int i = 0; i < 3675; i++)
        {
            if (FieldSkewLog.FirstTime(typeof(List<SkewedB>), "_syncRoot"))
                reported++;
        }

        Assert.Equal(1, reported);
    }
}
