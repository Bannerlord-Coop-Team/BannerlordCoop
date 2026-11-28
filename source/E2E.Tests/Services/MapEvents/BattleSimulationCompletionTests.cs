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
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Patches;
using GameInterface.Tests.Utils;
using HarmonyLib;
using Helpers;
using Missions.Messages;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
            // Even unscaled this hit can't down a troop, so the second hit still finds a standing one.
            combatModel.Setup(m => m.SimulateHit(It.IsAny<CharacterObject>(), It.IsAny<CharacterObject>(), It.IsAny<PartyBase>(),
                It.IsAny<PartyBase>(), It.IsAny<float>(), It.IsAny<MapEvent>(), It.IsAny<BattleEnvironment>(), It.IsAny<float>(), It.IsAny<float>()))
                .Returns(new ExplainedNumber(10));
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

                Assert.Equal(new[] { 5, 10 }, hitDamage);
            }
            finally
            {
                models.CombatSimulationModel = previousCombatModel;
                models.CombatXpModel = previousXpModel;
                models.DifficultyModel = previousDifficultyModel;
            }
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void SoloSiegeVictory_EntersOnServerAndGrantsGarrisonAccess()
    {
        var client = Clients.First();
        var context = CreateServerMapEvent();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var previousOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();
        const string controllerId = "solo-besieger";
        string? leaderId = null;

        Server.Call(() =>
        {
            var attacker = Server.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            Assert.True(Server.ObjectManager.TryGetId(attacker.LeaderHero, out leaderId));
        });
        Assert.NotNull(leaderId);
        RegisterAsPlayerParty(controllerId, leaderId, context.AttackerPartyId);
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);

        client.Call(() =>
        {
            using var scope = new AllowedThread();
            var leader = client.GetRegisteredObject<Hero>(leaderId);
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            Game.Current.PlayerTroop = leader.CharacterObject;
            var settlement = client.GetRegisteredObject<Settlement>(settlementId);
            settlement.SetSettlementComponent(client.GetRegisteredObject<Town>(townId));
        });

        try
        {
            Server.Call(() =>
            {
                var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
                var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
                var town = Server.GetRegisteredObject<Town>(townId);
                var previousOwner = Server.GetRegisteredObject<Hero>(previousOwnerId);
                previousOwner.Clan.SetLeader(previousOwner);
                settlement.SetSettlementComponent(town);
                town.OwnerClan = previousOwner.Clan;

                StartAcceptedSimulation(client, context);
                mapEvent.SetBattleType(MapEvent.BattleTypes.Siege);
                mapEvent.SetMapEventSettlement(settlement);
                mapEvent.BattleState = BattleState.AttackerVictory;

                var aftermath = new SiegeAftermathCampaignBehavior();
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, aftermath.OnMapEventEnded);
                CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(this, CaptureAndAttemptEarlyEntry);
                try
                {
                    AdvanceSimulation(client, context.MapEventId);

                    Assert.True(mapEvent.IsFinalized);
                    Assert.Same(settlement, Server.GetRegisteredObject<MobileParty>(context.AttackerPartyId).CurrentSettlement);
                }
                finally
                {
                    CampaignEvents.MapEventEnded.ClearListeners(this);
                    CampaignEvents.SiegeCompletedEvent.ClearListeners(this);
                    SiegeAftermathPatches.PendingAftermaths.TryRemove(settlement, out _);
                }
            }, MapEventDisabledMethods);

            // Exercise the real managed-menu reservation and consequence up to the party-screen presentation boundary.
            using var screen = new MethodCallRecorder(Priority.First,
                AccessTools.Method(typeof(PartyScreenHelper), nameof(PartyScreenHelper.OpenScreenAsManageTroops)));
            client.Call(() =>
            {
                var menu = SeedTownKeepMenu();
                Assert.Same(client.GetRegisteredObject<Settlement>(settlementId), Hero.MainHero.CurrentSettlement);
                menu.GameMenu.RunMenuOptionConsequence(menu, 0);
            });
            Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkSettlementMenuAccess>()).Granted);
            Assert.Equal(1, screen.CountFor(client));
        }
        finally
        {
            client.Call(() =>
            {
                SiegeCaptureTransitionRetryHandler.TryTakeReady(out _, out _);
                SiegeCaptureMenuHoldPatch.Release(client.GetRegisteredObject<Settlement>(settlementId));
            });
        }
    }

    // Reproduce the capture and early entry from SiegeCompleted without the campaign's NPC army logic.
    private static void CaptureAndAttemptEarlyEntry(Settlement settlement, MobileParty capturer, bool won, MapEvent.BattleTypes _)
    {
        if (!won) return;

        ChangeOwnerOfSettlementAction.ApplyBySiege(capturer.LeaderHero, capturer.LeaderHero, settlement);
        EnterSettlementAction.ApplyForParty(capturer, settlement);
    }

    private static MenuContext SeedTownKeepMenu()
    {
        var menus = Campaign.Current.GameMenuManager;
        var starter = new CampaignGameStarter(menus, Campaign.Current.ConversationManager);
        starter.AddGameMenu("town_keep", "", _ => { });
        starter.AddGameMenuOption("town_keep", "manage_garrison", "Manage garrison", _ => true,
            PlayerTownVisitCampaignBehavior.game_menu_manage_garrison_on_consequence);
        var menu = Game.Current.ObjectManager.CreateObject<MenuContext>();
        menu.GameMenu = menus.GetGameMenu("town_keep");
        var mapState = Game.Current.GameStateManager.CreateState<MapState>();
        mapState._menuContext = menu;
        Game.Current.GameStateManager._gameStates.Add(mapState);
        return menu;
    }

    /// <summary>Sends <see cref="MaxAdvancesToFinish"/> one-round advances from the client, enough for a decided battle to finish.</summary>
    private void AdvanceSimulation(EnvironmentInstance client, string mapEventId)
    {
        for (var update = 0; update < MaxAdvancesToFinish; update++)
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkAdvanceBattleSimulation(mapEventId, 1));
    }
}
