using GameInterface.Services.MapEvents;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Unit tests for <see cref="BattleSpawnGate"/> — the static state that <c>BattleSpawnDisablePatch</c> reads
/// to decide whether to run the vanilla troop spawner. Spawn itself can't run headlessly, so the gate's
/// host/non-host/unknown decision is covered directly here.
/// </summary>
public class BattleSpawnGateTests : IDisposable
{
    public void Dispose() => BattleSpawnGate.EndBattle();

    [Fact]
    public void NoActiveBattle_GateIsInactive_AndHostUnknown()
    {
        BattleSpawnGate.EndBattle();

        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        Assert.Null(BattleSpawnGate.ActiveMapEventId);
        Assert.Null(BattleSpawnGate.LocalIsHost);
    }

    [Fact]
    public void BeginBattle_WithUnknownHost_IsActive_ButHostStaysNull()
    {
        // Host election hasn't replied yet — the disable patch must withhold spawning (LocalIsHost == null).
        BattleSpawnGate.BeginBattle("mapEvent-1", isHost: null);

        Assert.True(BattleSpawnGate.IsCoopBattleActive);
        Assert.Equal("mapEvent-1", BattleSpawnGate.ActiveMapEventId);
        Assert.Null(BattleSpawnGate.LocalIsHost);
    }

    [Fact]
    public void SetLocalHost_ForActiveBattle_RecordsHostResult()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1", isHost: null);

        BattleSpawnGate.SetLocalHost("mapEvent-1", isHost: true);

        Assert.Equal(true, BattleSpawnGate.LocalIsHost);
    }

    [Fact]
    public void SetLocalHost_ForADifferentBattle_IsIgnored()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1", isHost: null);

        // An assignment for some other map event must not flip the active battle's host result.
        BattleSpawnGate.SetLocalHost("mapEvent-2", isHost: true);

        Assert.Null(BattleSpawnGate.LocalIsHost);
    }

    [Fact]
    public void EndBattle_ClearsActiveBattle_AndHostResult()
    {
        BattleSpawnGate.BeginBattle("mapEvent-1", isHost: true);

        BattleSpawnGate.EndBattle();

        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        Assert.Null(BattleSpawnGate.ActiveMapEventId);
        Assert.Null(BattleSpawnGate.LocalIsHost);
    }
}
