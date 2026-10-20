#if DEBUG
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Missions.Naval;
using Moq;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a: a returner with no reserve must leave its naval mission instead of hanging in deployment.</summary>
public class CoopNavalReserveGuardTests
{
    // Mission.AfterStart resets the state after EarlyStart, so the guard ends the mission from its first pre-display tick.
    [Fact]
    public void Guard_EndsTheMissionFromItsPreDisplayTick()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(CoopNavalReserveGuard), nameof(CoopNavalReserveGuard.OnPreDisplayMissionTick)));
    }

    [Fact]
    public void Target_ThePlayerTeamsInitialAllocationExists()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(NavalTeamSideSpawnContext),
            nameof(NavalTeamSideSpawnContext.AllocateAndDeployInitialTroopsOfPlayerTeam)));
    }

    // Vanilla assigns no ship to a team with no troops, and its allocation would put the player troop on that missing hull.
    [Fact]
    public void ShiplessPlayerTeam_SkipsTheInitialAllocation()
    {
        var unset = (ShipAssignment)RuntimeHelpers.GetUninitializedObject(typeof(ShipAssignment));

        Assert.False(CoopNavalShiplessPlayerTeamPatch.AllocatesPlayerTeam(null));
        Assert.False(CoopNavalShiplessPlayerTeamPatch.AllocatesPlayerTeam(unset));
    }

    // R41a fix-loop C5: a shipless player team asks the guard to end the mission each tick vanilla retries the allocation, and
    // the player is told once.
    [Fact]
    public void RequestEnd_OnlyTheFirstRequestEndsTheMissionAndTellsThePlayer()
    {
        var broker = new Mock<IMessageBroker>();
        var supplier = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, Mock.Of<IObjectManager>(), Mock.Of<IBattleAgentBudget>());
        var guard = new CoopNavalReserveGuard(supplier, BattleSideEnum.Attacker, broker.Object);

        bool first = guard.RequestEnd();
        bool second = guard.RequestEnd();

        Assert.True(first);
        Assert.False(second);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
    }
}
#endif
