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
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// K5: a naval battle fields a promoted host's recovered reserve on its hulls, so it takes the reinforcement fielder's queued
/// migration recovery instead of the land spawn path, minus troops a late replay already brought in and parties that left
/// this host's reserve scope.
/// </summary>
public class ReinforcementFielderMigrationReserveTests : MissionTestEnvironment
{
    public ReinforcementFielderMigrationReserveTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void TakeMigrationReserve_HandsOverTheSidesQueue_OnceAndWithoutLiveSeedsOrPartiesNoLongerInScope()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        client.Call(() =>
        {
            fixture.CreateMission(client);
            var registry = client.Resolve<INetworkAgentRegistry>();
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(value => value.AgentRegistry).Returns(registry);
            var casualties = new CasualtyAttributionMap();
            using var fielder = new ReinforcementFielder(client.Resolve<Common.Messaging.IMessageBroker>(), client.ObjectManager,
                component.Object, Mock.Of<IBattleSession>(), Mock.Of<IBattleDeploymentCoordinator>(),
                Mock.Of<IAgentFormationAssigner>(), casualties, new BattleAgentBudget());
            var character = (CharacterObject)Game.Current.PlayerTroop;
            // The reserve now holds only the pirates; the returned player's party left this host's scope.
            var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, client.ObjectManager, new BattleAgentBudget());
            supplier.SetReserve(new[] { new PartyReserve("pirates", 3, new[] { new TroopReserveEntry(1, "troop", 0) }) },
                sideTotal: 3, playerOwnedParties: 0, authoritativeBattleSize: 1000);
            QueueRecovery(fielder, supplier, BattleSideEnum.Defender, "pirates",
                Enumerable.Range(1, 3).Select(seed => new CoopAgentOrigin(character, null, 0, null, new UniqueTroopDescriptor(seed), "pirates")));
            QueueRecovery(fielder, supplier, BattleSideEnum.Defender, "returned",
                new[] { new CoopAgentOrigin(character, null, 0, null, new UniqueTroopDescriptor(7), "returned") });

            // Seed 2 already came back as a replayed agent.
            var replayed = Mission.Current.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            var replayedId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("host", replayedId, replayed));
            casualties.Record(replayedId, "pirates", 2, "troop");

            var taken = fielder.TakeMigrationReserve(BattleSideEnum.Defender);

            Assert.Equal(new[] { 1, 3 }, taken.Select(origin => origin.UniqueSeed));
            Assert.Empty(fielder.TakeMigrationReserve(BattleSideEnum.Defender));
            Assert.Empty(fielder.TakeMigrationReserve(BattleSideEnum.Attacker));
            Assert.Empty(fielder.TakeMigrationReserve(BattleSideEnum.None));
        });
    }

    // M7: the naval queue rechecks taken troops with the same party and seed rule, so a replay after the take is caught.
    [Fact]
    public void RemoveLateReplayed_RemovesOnlyALiveAgentWithTheOriginsPartyAndSeed()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        client.Call(() =>
        {
            fixture.CreateMission(client);
            var registry = client.Resolve<INetworkAgentRegistry>();
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(value => value.AgentRegistry).Returns(registry);
            var casualties = new CasualtyAttributionMap();
            using var fielder = new ReinforcementFielder(client.Resolve<Common.Messaging.IMessageBroker>(), client.ObjectManager,
                component.Object, Mock.Of<IBattleSession>(), Mock.Of<IBattleDeploymentCoordinator>(),
                Mock.Of<IAgentFormationAssigner>(), casualties, new BattleAgentBudget());
            var character = (CharacterObject)Game.Current.PlayerTroop;
            var replayedOrigin = new CoopAgentOrigin(character, null, 0, null, new UniqueTroopDescriptor(2), "pirates");
            var otherSeed = new CoopAgentOrigin(character, null, 0, null, new UniqueTroopDescriptor(3), "pirates");
            var otherParty = new CoopAgentOrigin(character, null, 0, null, new UniqueTroopDescriptor(2), "raiders");
            var waiting = new List<CoopAgentOrigin> { replayedOrigin };
            Assert.Equal(0, fielder.RemoveLateReplayed(waiting));

            var replayed = Mission.Current.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            var replayedId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("host", replayedId, replayed));
            casualties.Record(replayedId, "pirates", 2, "troop");
            waiting = new List<CoopAgentOrigin> { replayedOrigin, otherSeed, otherParty };

            Assert.Equal(1, fielder.RemoveLateReplayed(waiting));
            Assert.Equal(new[] { otherSeed, otherParty }, waiting);
        });
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
