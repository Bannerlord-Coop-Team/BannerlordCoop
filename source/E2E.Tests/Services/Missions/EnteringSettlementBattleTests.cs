using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using Moq;
using SandBox.Missions.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

public sealed class EnteringSettlementBattleTests
{
    [Theory]
    [InlineData(-2, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    [InlineData(1, 7)]
    public void ControllableTroopsFollowTheOwningMissionTeam(int side, int expected)
    {
        var previousEnabled = BattleSpawnConfig.Enabled;
        var harmony = new Harmony("e2e.entering-settlement-troops");
        var mission = ObjectHelper.SkipConstructor<Mission>();
        GC.SuppressFinalize(mission);
        mission.Teams = new Mission.TeamCollection(mission);
        if (side != -2)
        {
            var team = ObjectHelper.SkipConstructor<Team>();
            GC.SuppressFinalize(team);
            // The readonly auto-property has no setter to publicize.
            AccessTools.Field(typeof(Team), "<Side>k__BackingField").SetValue(team, (BattleSideEnum)side);
            mission.Teams._playerTeam = team;
        }
        var defender = new Mock<IMissionTroopSupplier>();
        var attacker = new Mock<IMissionTroopSupplier>();
        defender.Setup(supplier => supplier.GetNumberOfPlayerControllableTroops()).Returns(3);
        attacker.Setup(supplier => supplier.GetNumberOfPlayerControllableTroops()).Returns(7);
        BattleSpawnGate.EndBattle();
        var controller = new WhileEnteringSettlementBattleMissionController(
            new[] { defender.Object, attacker.Object }, 10, 10) { Mission = mission };
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(EnteringSettlementBattlePatches)).Patch());
            BattleSpawnConfig.Enabled = true;
            BattleSpawnGate.BeginBattle("entering-settlement-query");

            Assert.Equal(expected, controller.GetNumberOfPlayerControllableTroops());
            defender.Verify(supplier => supplier.GetNumberOfPlayerControllableTroops(),
                side == 0 ? Times.Once() : Times.Never());
            attacker.Verify(supplier => supplier.GetNumberOfPlayerControllableTroops(),
                side == 1 ? Times.Once() : Times.Never());

            BattleSpawnGate.EndBattle();
            Assert.Throws<NotImplementedException>(() => controller.GetNumberOfPlayerControllableTroops());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            BattleSpawnGate.EndBattle();
            BattleSpawnConfig.Enabled = previousEnabled;
        }
    }
}
