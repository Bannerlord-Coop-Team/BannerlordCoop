using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Moq;
using System.Collections;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// #4650: a siege assault spawns every troop on foot (the spawn handler turns both sides' horses off). Troops the
/// reinforcement fielder brings back after an authority migration must follow the same side setting, or cavalry rides
/// into the siege and blocks the gates.
/// </summary>
public class ReinforcementFielderSiegeHorsesTests : MissionTestEnvironment
{
    public ReinforcementFielderSiegeHorsesTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void FieldRecoverySide_SpawnsAHorseOnlyWhenTheSideSpawnsHorses(bool sideSpawnsHorses, int expectedMounts)
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("host", "peer");
        var client = Clients.First();
        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var registry = client.Resolve<INetworkAgentRegistry>();
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(value => value.AgentRegistry).Returns(registry);
            using var fielder = new ReinforcementFielder(client.Resolve<Common.Messaging.IMessageBroker>(), client.ObjectManager,
                component.Object, Mock.Of<IBattleSession>(), Mock.Of<IBattleDeploymentCoordinator>(),
                Mock.Of<IAgentFormationAssigner>(), new CasualtyAttributionMap(), new BattleAgentBudget());
            var character = (CharacterObject)Game.Current.PlayerTroop;
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var party));
            var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, client.ObjectManager, new BattleAgentBudget());
            supplier.SetReserve(new[] { new PartyReserve("besiegers", 1, new[] { new TroopReserveEntry(1, "troop", 0) }) },
                sideTotal: 1, playerOwnedParties: 0, authoritativeBattleSize: 1000);
            QueueRecovery(fielder, supplier, BattleSideEnum.Attacker, "besiegers",
                new[] { new CoopAgentOrigin(character, party.Party, 0, null, new UniqueTroopDescriptor(1), "besiegers") });
            mock.SpawnMounted = true; // the troop's equipment would mount a horse

            int spawned = FieldRecoverySide(fielder, BattleSideEnum.Attacker, available: 1, sideSpawnsHorses);

            Assert.Equal(1, spawned);
            Assert.Equal(expectedMounts, Mission.Current.Agents.Count(agent => agent.IsMount));
        });
    }

    private static int FieldRecoverySide(ReinforcementFielder fielder, BattleSideEnum side, int available, bool sideSpawnsHorses)
    {
        var method = AccessTools.Method(typeof(ReinforcementFielder), "FieldRecoverySide");
        return (int)method.Invoke(fielder, new object[] { side, available, new Dictionary<Formation, bool>(), sideSpawnsHorses })!;
    }

    // The queue TryQueueMigrationReserves builds from the roster once both reserve replies arrived.
    private static void QueueRecovery(ReinforcementFielder fielder, CoopTroopSupplier supplier, BattleSideEnum side, string partyId,
        IEnumerable<CoopAgentOrigin> origins)
    {
        var recoveryType = typeof(ReinforcementFielder).GetNestedType("RecoveryParty", BindingFlags.NonPublic)!;
        var recovery = Activator.CreateInstance(recoveryType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object?[] { supplier, partyId, new Queue<CoopAgentOrigin>(origins) }, null);
        var queues = (IList[])AccessTools.Field(typeof(ReinforcementFielder), "recoveryParties").GetValue(fielder)!;
        queues[(int)side].Add(recovery);
        var partyIds = (HashSet<string>)AccessTools.Field(typeof(ReinforcementFielder), "recoveryPartyIds").GetValue(fielder)!;
        partyIds.Add(partyId);
    }
}
