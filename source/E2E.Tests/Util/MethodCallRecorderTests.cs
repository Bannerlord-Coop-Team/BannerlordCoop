using HarmonyLib;
using System.Runtime.CompilerServices;
using Xunit;

namespace E2E.Tests.Util;

public class MethodCallRecorderTests
{
    [Fact]
    public void DisposeClearsCallsAndRestoresOnlyItsOwnMethods()
    {
        var firstMethod = AccessTools.Method(typeof(MethodCallRecorderTests), nameof(FirstCall));
        var secondMethod = AccessTools.Method(typeof(MethodCallRecorderTests), nameof(SecondCall));
        using var first = new MethodCallRecorder(firstMethod);
        using var second = new MethodCallRecorder(secondMethod);

        FirstCall();
        SecondCall("encounter");
        Assert.Equal(1, first.Count);
        Assert.Equal(1, second.Count);

        first.Dispose();
        Assert.Equal(0, first.Count);
        Assert.Throws<InvalidOperationException>(FirstCall);
        SecondCall("encounter");
        Assert.Equal(2, second.Count);

        using var next = new MethodCallRecorder(firstMethod);
        FirstCall();
        Assert.Equal(1, next.Count);
        Assert.Equal(0, first.Count);
        second.Dispose();
        Assert.Equal(0, second.Count);
        Assert.Throws<InvalidOperationException>(() => SecondCall("encounter"));
    }

    [Fact]
    public void FailedConstructionReleasesNewPatchesWithoutDisturbingExistingRecorder()
    {
        var firstMethod = AccessTools.Method(typeof(MethodCallRecorderTests), nameof(FirstCall));
        var secondMethod = AccessTools.Method(typeof(MethodCallRecorderTests), nameof(SecondCall));
        using var existing = new MethodCallRecorder(firstMethod);

        Assert.Throws<ArgumentException>(() => new MethodCallRecorder(secondMethod, firstMethod));

        Assert.Throws<InvalidOperationException>(() => SecondCall("encounter"));
        FirstCall();
        Assert.Equal(1, existing.Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FirstCall() => throw new InvalidOperationException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SecondCall(string menuId) => throw new InvalidOperationException();
}
