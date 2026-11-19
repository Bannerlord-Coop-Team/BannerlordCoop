using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Participation;
using GameInterface.Services.Players;
using Missions.Messages;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Exercises the shared simulation teardown instead of substituting direct map-event finalization.
/// </summary>
public class BattleSimulationCompletionTests : MapEventTestBase
{
    private const int MaxAdvancesToFinish = 200;

    public BattleSimulationCompletionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CompletedAdvance_SendsLootBeforeRemovingMapEvent_AndFinishesOnce()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        var lootSawRegisteredEvent = new List<bool>();
        client.Resolve<IMessageBroker>().Subscribe<NetworkBattleSimulationLoot>(payload =>
            lootSawRegisteredEvent.Add(client.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _)));

        Server.Call(() =>
        {
            StartAcceptedSimulation(client, context);
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            mapEvent.BattleState = BattleState.AttackerVictory;
            var ended = new List<MapEvent>();
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            try
            {
                Server.NetworkSentMessages.Clear();
                AdvanceSimulation(client, context.MapEventId);

                Assert.True(mapEvent.IsFinalized);
                Assert.Same(mapEvent, Assert.Single(ended));
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationLoot>());
                Assert.Equal((int)BattleStartMode.Unclaimed,
                    Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>()).Mode);
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
                Assert.False(ServerBattleModeArbiter.IsClaimed(context.MapEventId));
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(this);
            }
        }, MapEventDisabledMethods);

        Assert.True(Assert.Single(lootSawRegisteredEvent));

        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _)));
    }

    [Fact]
    public void RetreatedPlayerStartsSimulation_AndWins_StillReceivesWinnerLoot()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            var playerParty = Server.GetRegisteredObject<MobileParty>(context.AttackerPartyId).Party;
            Server.Resolve<IMessageBroker>().Publish(this, new BattlePartyRetreated("1", context.MapEventId));
            Assert.True(Server.Resolve<IRetreatedMapEventPartyTracker>().IsRetreated(mapEvent, playerParty));

            StartAcceptedSimulation(client, context);

            mapEvent.BattleState = BattleState.AttackerVictory;
            AdvanceSimulation(client, context.MapEventId);

            var results = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkCommitMapEventResults>());
            // Only parties kept in the winners get a loot entry, even when it's empty.
            Assert.Contains(results.PlayerMapEventPartyId, results.PlayerLootData.LootedItems.Keys);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void SimulatedHit_OnServer_ScalesOnlyPlayerPartyDamageByDifficulty()
    {
        var context = CreateServerMapEvent();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            var troop = Server.GetRegisteredObject<CharacterObject>(troopId);
            // The hit reward reads the striker's weapon slot, so both sides only hold this equipped troop.
            troop.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
            using (new AllowedThread())
            {
                foreach (var partyId in new[] { context.AttackerPartyId, context.DefenderPartyId })
                {
                    var roster = Server.GetRegisteredObject<MobileParty>(partyId).MemberRoster;
                    roster.Clear();
                    roster.AddToCounts(troop, 5);
                }
            }
            mapEvent.SimulateBattleSetup(null);

            var models = Campaign.Current.Models;
            var previousCombatModel = models.CombatSimulationModel;
            var previousXpModel = models.CombatXpModel;
            var previousDifficultyModel = models.DifficultyModel;
            var combatModel = new Mock<CombatSimulationModel>();
            combatModel.Setup(m => m.SimulateHit(It.IsAny<CharacterObject>(), It.IsAny<CharacterObject>(), It.IsAny<PartyBase>(),
                It.IsAny<PartyBase>(), It.IsAny<float>(), It.IsAny<MapEvent>(), It.IsAny<float>(), It.IsAny<float>()))
                .Returns(new ExplainedNumber(40));
            // The striker's hit reward gets the same damage the struck troop takes.
            var hitDamage = new List<int>();
            var xpModel = new Mock<CombatXpModel>();
            xpModel.Setup(m => m.GetXpFromHit(It.IsAny<CharacterObject>(), It.IsAny<CharacterObject>(), It.IsAny<CharacterObject>(),
                It.IsAny<PartyBase>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CombatXpModel.MissionTypeEnum>()))
                .Callback<CharacterObject, CharacterObject, CharacterObject, PartyBase, int, bool, CombatXpModel.MissionTypeEnum>(
                    (_, _, _, _, damage, _, _) => hitDamage.Add(damage));
            try
            {
                models.CombatSimulationModel = combatModel.Object;
                models.CombatXpModel = xpModel.Object;
                models.DifficultyModel = Mock.Of<DifficultyModel>(m => m.GetPlayerTroopsReceivedDamageMultiplier() == 0.5f);

                mapEvent.SimulateSingleTroopHit(BattleSideEnum.Defender, 0f, 1f, 1f);
                mapEvent.SimulateSingleTroopHit(BattleSideEnum.Attacker, 0f, 1f, 1f);

                Assert.Equal(new[] { 20, 40 }, hitDamage);
            }
            finally
            {
                models.CombatSimulationModel = previousCombatModel;
                models.CombatXpModel = previousXpModel;
                models.DifficultyModel = previousDifficultyModel;
            }
        }, MapEventDisabledMethods);
    }

    /// <summary>Sends <see cref="MaxAdvancesToFinish"/> one-round advances from the client, enough for a decided battle to finish.</summary>
    private void AdvanceSimulation(EnvironmentInstance client, string mapEventId)
    {
        for (var update = 0; update < MaxAdvancesToFinish; update++)
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkAdvanceBattleSimulation(mapEventId, 1));
    }
}
