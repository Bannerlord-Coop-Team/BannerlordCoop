using GameInterface.Services.MapEvents;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Unit tests for <see cref="BattleSpawnGate"/> — the static flag the (GameInterface) battle-spawn patches read
/// to know a coop field battle is the active mission. It holds no host/ownership state (which client fields which
/// troops is decided server-side by the reserve assignment), so only the active/map-event lifecycle is covered.
/// </summary>
public class BattleSpawnGateTests : IDisposable
{
    public void Dispose() => BattleSpawnGate.EndBattle();

    [Fact]
    public void NoActiveBattle_GateIsInactive()
    {
        BattleSpawnGate.EndBattle();

        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        Assert.Null(BattleSpawnGate.ActiveMapEventId);
    }

    [Fact]
    public void BeginBattle_MarksActive_WithTheMapEvent()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1");

        Assert.True(BattleSpawnGate.IsCoopBattleActive);
        Assert.Equal("mapEvent-1", BattleSpawnGate.ActiveMapEventId);
    }

    [Fact]
    public void EndBattle_ClearsActiveBattle()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1");

        BattleSpawnGate.EndBattle();

        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        Assert.Null(BattleSpawnGate.ActiveMapEventId);
    }

    [Fact]
    public void MissingReserveAcceptance_IsPerSide_AndClearedForNextBattle()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1");
        BattleSpawnGate.AcceptMissingReserveSide(BattleSideEnum.Defender);

        Assert.True(BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Defender));
        Assert.False(BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Attacker));

        BattleSpawnGate.BeginBattle("mapEvent-2");

        Assert.False(BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Defender));
        Assert.False(BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Attacker));
    }
}
