#if DEBUG
using System;
using System.Linq;
using Common;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabPresentationLifecycleTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.presentation-lifecycle");
    private readonly NavalLabBehavior fixture;
    private static bool ready;
    public NavalLabPresentationLifecycleTests()
    {
        ready = true;
        harmony.Patch(AccessTools.PropertyGetter(typeof(NavalLabBehavior), "PresentationReady"),
            prefix: new HarmonyMethod(GetType(), nameof(Ready)));
        harmony.Patch(AccessTools.Method(typeof(NavalLabBehavior), "PreparePresentationInventory"),
            prefix: new HarmonyMethod(GetType(), nameof(Ready)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(GameThread), nameof(GameThread.IsGameThread)),
            prefix: new HarmonyMethod(GetType(), nameof(OnGameThread)));
        var id = Guid.NewGuid();
        fixture = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.TwoClientNative), "A", null!, null!);
    }
    private static bool Ready(ref bool __result) { __result = ready; return false; }
    private static bool OnGameThread(ref bool __result) { __result = true; return false; }
    private NetworkNavalLabFrames Frame(long sequence, float x, long? deadline = null, int epoch = 1) => new(
        fixture.manifest.IncarnationId, epoch, sequence, new float[24], sequence,
        sailDeadlineUtcTicks: deadline ?? DateTime.UtcNow.AddSeconds(1).Ticks,
        presentation: fixture.manifest.Ships.Select(ship => NavalLabPresentationTests.State(ship, x / 10, x)).ToArray());

    [Fact]
    public void BaselineIsDirectAndSupersessionStartsFromLastPublishedPose()
    {
        fixture.AcceptPresentation(Frame(1, 0));
        Assert.Equal(1, fixture.displayedPresentation.Sequence);
        fixture.AcceptPresentation(Frame(2, 2));
        fixture.TickPresentation(0.025f);
        Assert.Equal(1, fixture.displayedPresentation.Ships[0].Oars[0].BladeFrame[9], 4);
        fixture.AcceptPresentation(Frame(3, 3));
        fixture.TickPresentation(0.025f);
        Assert.Equal(2, fixture.displayedPresentation.Ships[0].Oars[0].BladeFrame[9], 4);
        Assert.Equal(3, fixture.displayedPresentation.Sequence);
    }

    [Fact]
    public void ExpiredOldOrWrongEpochFrameCannotEraseNewerDisplay()
    {
        fixture.AcceptPresentation(Frame(5, 2));
        var shown = fixture.displayedPresentation;
        foreach (var rejected in new[] { Frame(4, 5), Frame(6, 5, DateTime.UtcNow.AddSeconds(-1).Ticks), Frame(6, 5, epoch: 2) })
        {
            Assert.False(fixture.ValidatePresentation(rejected));
            fixture.AcceptPresentation(rejected);
            Assert.Same(shown, fixture.displayedPresentation);
            Assert.Equal(5, fixture.presentationSequence);
        }
    }

    [Fact]
    public void StaleVisualHoldsWithoutExtrapolationAndTerminalClears()
    {
        fixture.AcceptPresentation(Frame(1, 0));
        fixture.AcceptPresentation(Frame(2, 3));
        fixture.TickPresentation(0.02f);
        var shown = fixture.displayedPresentation;
        fixture.presentationDeadline = 0;
        fixture.TickPresentation(1);
        Assert.Same(shown, fixture.displayedPresentation);
        ready = false;
        fixture.TickPresentation(0.01f);
        Assert.Null(fixture.displayedPresentation);
        Assert.Null(fixture.presentationTarget);
    }
    public void Dispose() => harmony.UnpatchAll(harmony.Id);
}
#endif
