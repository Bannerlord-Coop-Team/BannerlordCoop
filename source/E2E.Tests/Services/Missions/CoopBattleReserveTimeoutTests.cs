using System.Linq;
using System.Runtime.Serialization;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions.Battles;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Checks that a reserve timeout the spawn handler accepts reaches <see cref="CoopBattleDepletionPatch"/> through
/// the same instance's battle mission registry, and no other instance's.
/// </summary>
public class CoopBattleReserveTimeoutTests : MissionTestEnvironment
{
    private const string MapEventId = "reserve-timeout-battle";

    public CoopBattleReserveTimeoutTests(ITestOutputHelper output) : base(output) { }

    // A defender reserve timeout accepted on one instance depletes that side there and nowhere else.
    [Fact]
    public void AcceptedMissingReserve_IsDepletedOnlyOnTheAcceptingInstance()
    {
        using var fixture = new MissionEngineFixture();
        var accepting = Clients.First();
        var other = Clients.Last();
        BeginCoopBattle(MapEventId, accepting, other);

        accepting.Call(() =>
        {
            fixture.CreateMission(accepting);
            var handler = new CoopBattleMissionSpawnHandler(
                new CoopTroopSupplier(MapEventId, BattleSideEnum.Defender, null, new BattleAgentBudget()),
                new CoopTroopSupplier(MapEventId, BattleSideEnum.Attacker, null, new BattleAgentBudget()),
                null, accepting.Resolve<ICoopBattleMissionRegistry>(), BattleSideEnum.Attacker);
            var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
                defenderPopulated: false, attackerPopulated: true, defenderOwned: 0, attackerOwned: 5,
                battleSize: 1000);
            handler.AcceptMissingReserveSides(sizing);

            Assert.True(IsSideDepleted(BattleSideEnum.Defender));
            Assert.False(IsSideDepleted(BattleSideEnum.Attacker));
        });

        other.Call(() =>
        {
            fixture.CreateMission(other);
            Assert.False(IsSideDepleted(BattleSideEnum.Defender));
        });
    }

    // Runs the depletion prefix against a spawn logic whose sides never fielded an agent and returns its verdict.
    private static bool IsSideDepleted(BattleSideEnum side)
    {
#pragma warning disable SYSLIB0050 // Identity-only spawn logic; the prefix only keys a weak table on it.
        var spawnLogic = (DefaultBattleMissionAgentSpawnLogic)FormatterServices.GetUninitializedObject(
            typeof(DefaultBattleMissionAgentSpawnLogic));
#pragma warning restore SYSLIB0050
        var args = new object[] { spawnLogic, side, false };
        bool runNative = (bool)AccessTools.Method(typeof(CoopBattleDepletionPatch), "Prefix").Invoke(null, args);

        Assert.False(runNative);
        return (bool)args[2];
    }
}
