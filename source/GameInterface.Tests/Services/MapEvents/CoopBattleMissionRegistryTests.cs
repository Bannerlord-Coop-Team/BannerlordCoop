using GameInterface.Services.MapEvents;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests the loaded-mission and battle windows of <see cref="CoopBattleMissionRegistry"/>.</summary>
public class CoopBattleMissionRegistryTests
{
    // A fresh registry reports no coop battle mission, matching the vanilla default.
    [Fact]
    public void IsMissionActive_FalseBeforeAnyMissionBegins()
    {
        var registry = new CoopBattleMissionRegistry();

        Assert.False(registry.IsMissionActive);
    }

    // Beginning and ending the same mission opens and closes the window.
    [Fact]
    public void EndMission_SameId_ClosesWindow()
    {
        var registry = new CoopBattleMissionRegistry();

        registry.BeginMission("MapEvent_1");
        Assert.True(registry.IsMissionActive);

        registry.EndMission("MapEvent_1");
        Assert.False(registry.IsMissionActive);
    }

    // A stale controller disposed after a newer mission began must not close the newer window.
    [Fact]
    public void EndMission_StaleId_KeepsNewerWindowOpen()
    {
        var registry = new CoopBattleMissionRegistry();

        registry.BeginMission("MapEvent_1");
        registry.BeginMission("MapEvent_2");
        registry.EndMission("MapEvent_1");

        Assert.True(registry.IsMissionActive);

        registry.EndMission("MapEvent_2");
        Assert.False(registry.IsMissionActive);
    }

    // A mission that loaded before its instance id was known still opens and closes its window.
    [Fact]
    public void EndMission_NullId_ClosesWindowBegunWithNullId()
    {
        var registry = new CoopBattleMissionRegistry();

        registry.BeginMission(null);
        Assert.True(registry.IsMissionActive);

        registry.EndMission(null);
        Assert.False(registry.IsMissionActive);
    }

    // A fresh registry reports no coop battle, so every battle patch runs vanilla.
    [Fact]
    public void IsCoopBattleActive_FalseBeforeAnyBattleBegins()
    {
        var registry = new CoopBattleMissionRegistry();

        Assert.False(registry.IsCoopBattleActive);
        Assert.Null(registry.ActiveMapEventId);
    }

    // Beginning a battle marks it active with its map event.
    [Fact]
    public void BeginBattle_MarksActive_WithTheMapEvent()
    {
        var registry = new CoopBattleMissionRegistry();

        registry.BeginBattle("mapEvent-1");

        Assert.True(registry.IsCoopBattleActive);
        Assert.Equal("mapEvent-1", registry.ActiveMapEventId);
    }

    // A repeated teardown neither throws nor resurrects the battle.
    [Fact]
    [Trait("Requirement", "BR-091")]
    public void EndBattle_ClearsActiveBattle_Idempotently()
    {
        var registry = new CoopBattleMissionRegistry();
        registry.BeginBattle("mapEvent-1");

        registry.EndBattle();

        Assert.False(registry.IsCoopBattleActive);
        Assert.Null(registry.ActiveMapEventId);

        registry.EndBattle();

        Assert.False(registry.IsCoopBattleActive);
        Assert.Null(registry.ActiveMapEventId);
    }

    // The battle window and the loaded-mission window open and close independently.
    [Fact]
    public void BattleWindow_IsIndependentOfMissionWindow()
    {
        var registry = new CoopBattleMissionRegistry();

        registry.BeginBattle("mapEvent-1");
        Assert.False(registry.IsMissionActive);

        registry.BeginMission("mapEvent-1");
        registry.EndBattle();
        Assert.True(registry.IsMissionActive);
        Assert.False(registry.IsCoopBattleActive);
    }

    // Reserve timeouts are tracked per side and cleared when the next battle begins.
    [Fact]
    public void MissingReserveAcceptance_IsPerSide_AndClearedForNextBattle()
    {
        var registry = new CoopBattleMissionRegistry();
        registry.BeginBattle("mapEvent-1");
        registry.AcceptMissingReserveSide(BattleSideEnum.Defender);

        Assert.True(registry.IsMissingReserveSideAccepted(BattleSideEnum.Defender));
        Assert.False(registry.IsMissingReserveSideAccepted(BattleSideEnum.Attacker));

        registry.RestoreReserveSide(BattleSideEnum.Defender);
        Assert.False(registry.IsMissingReserveSideAccepted(BattleSideEnum.Defender));

        registry.AcceptMissingReserveSide(BattleSideEnum.Defender);

        registry.BeginBattle("mapEvent-2");

        Assert.False(registry.IsMissingReserveSideAccepted(BattleSideEnum.Defender));
        Assert.False(registry.IsMissingReserveSideAccepted(BattleSideEnum.Attacker));
    }

    // Ending the battle also forgets any reserve timeout it accepted.
    [Fact]
    public void EndBattle_ClearsMissingReserveAcceptance()
    {
        var registry = new CoopBattleMissionRegistry();
        registry.BeginBattle("mapEvent-1");
        registry.AcceptMissingReserveSide(BattleSideEnum.Attacker);

        registry.EndBattle();

        Assert.False(registry.IsMissingReserveSideAccepted(BattleSideEnum.Attacker));
    }
}
