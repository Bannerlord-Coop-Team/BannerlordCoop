using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using Moq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// "Assault the siege camp" from a besieged settlement's menu (#4105, #4137).
/// </summary>
public class SiegeCampAssaultTests : MapEventTestBase
{
    private const string ControllerId = "PlayerOne";

    private static readonly MethodInfo StartBattleInternal =
        AccessTools.Method(typeof(PlayerEncounter), "StartBattleInternal");

    private IReadOnlyList<MethodBase> StagingDisabledMethods => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)))
        .Append(AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)))
        .Append(AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)))
        .Append(AccessTools.Method(typeof(ChangeRelationAction), "ApplyInternal"))
        // Activating a menu needs a rendered map state. The encounter menu init is the StartBattle call below.
        .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }))
        .ToList();

    private IReadOnlyList<MethodBase> DisabledMethods => StagingDisabledMethods
        .Concat(WithoutNetworkDelivery())
        .ToList();

    public SiegeCampAssaultTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssaultSiegeCamp_FriendlySettlement_AttacksCampNotSettlement(bool ownClan)
    {
        var client = Clients.First();
        var context = CreateContext(client, ownClan ? OwnerStance.OwnClan : OwnerStance.Neutral);
        string? playerId = null;
        string? campId = null;

        client.Call(() =>
        {
            var encounter = ChooseAssaultSiegeCamp(client, context);

            Assert.Null(StartBattleInternal.Invoke(encounter, Array.Empty<object>()));

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.CampLeaderId, out var campLeader));
            Assert.Same(campLeader.Party, encounter._defenderParty);
            Assert.Equal(BattleSideEnum.Attacker, encounter.PlayerSide);
            Assert.True(client.ObjectManager.TryGetId(encounter._attackerParty, out playerId));
            Assert.True(client.ObjectManager.TryGetId(campLeader.Party, out campId));
        }, DisabledMethods);

        var restart = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        Assert.Equal(campId, restart.DefenderId);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
        Assert.Equal(playerId, request.AttackerId);
        Assert.Equal(campId, request.DefenderId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssaultSiegeCamp_DelayedRestartApproval_KeepsCampEncounter(bool duringBattleRequest)
    {
        var client = Clients.First();
        var context = CreateContext(client, OwnerStance.OwnClan);
        PlayerEncounter? encounter = null;

        client.Call(() => encounter = ChooseAssaultSiegeCamp(client, context), DisabledMethods);

        var restart = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        var approval = new NetworkAllowConversation(
            restart.DefenderId,
            restart.AttackerId,
            restart.ForcePlayerOutFromSettlement,
            restart.Source,
            restart.RequestId);

        using var restarts = new MethodCallRecorder(Priority.Normal, true,
            AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.RestartPlayerEncounter)));
        client.Call(() =>
        {
            var broker = client.Resolve<IMessageBroker>();
            var approvalDelivered = false;
            if (duringBattleRequest)
            {
                // Runs inside the battle request's WaitWhilePumping, after the retarget.
                GameThread.EnqueueSafe(() =>
                {
                    broker.Publish(null, approval);
                    approvalDelivered = true;
                });
            }

            Assert.Null(StartBattleInternal.Invoke(encounter, Array.Empty<object>()));

            if (!duringBattleRequest)
            {
                broker.Publish(null, approval);
                GameThread.Instance.Update(TimeSpan.Zero);
                approvalDelivered = true;
            }

            Assert.True(approvalDelivered);
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.CampLeaderId, out var campLeader));
            Assert.Same(encounter, PlayerEncounter.Current);
            Assert.Same(campLeader.Party, encounter!._defenderParty);
        }, DisabledMethods);

        Assert.Equal(0, restarts.CountFor(client));
    }

    [Fact]
    public void AssaultSiegeCamp_CampLeaderInBattle_KeepsSettlementTarget()
    {
        var client = Clients.First();
        var context = CreateContext(client, OwnerStance.OwnClan);
        var battle = CreateServerMapEvent();
        string? settlementId = null;

        client.Call(() =>
        {
            var encounter = StageSettlementEncounter(client, context);
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.CampLeaderId, out var campLeader));
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            campLeader.Party._mapEventSide = mapEvent.DefenderSide;

            Assert.Null(StartBattleInternal.Invoke(encounter, Array.Empty<object>()));

            Assert.True(encounter._defenderParty.IsSettlement);
            Assert.True(client.ObjectManager.TryGetId(encounter._defenderParty, out settlementId));
        }, DisabledMethods);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
        Assert.Equal(settlementId, request.DefenderId);
    }

    [Fact]
    public void AssaultSiegeCamp_HostileSettlement_KeepsSettlementTarget()
    {
        var client = Clients.First();
        var context = CreateContext(client, OwnerStance.Hostile);
        string? settlementId = null;

        client.Call(() =>
        {
            var encounter = StageSettlementEncounter(client, context);

            Assert.Null(StartBattleInternal.Invoke(encounter, Array.Empty<object>()));

            Assert.True(encounter._defenderParty.IsSettlement);
            Assert.True(client.ObjectManager.TryGetId(encounter._defenderParty, out settlementId));
        }, DisabledMethods);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
        Assert.Equal(settlementId, request.DefenderId);
    }

    [Fact]
    public void ServerMapEventRequest_FriendlyBesiegedSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateContext(client, OwnerStance.OwnClan);
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        Server.Resolve<IPlayerManager>().SetPeer(ControllerId, client.NetPeer);
        string? playerId = null;
        string? settlementId = null;

        Server.Call(() =>
        {
            var settlement = StageSiege(Server, context);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out playerId));
            Assert.True(Server.ObjectManager.TryGetId(settlement.Party, out settlementId));
        }, StagingDisabledMethods);
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestCreateMapEvent(Guid.NewGuid().ToString(), playerId, settlementId, default, null)));

        var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventCreated>());
        Assert.Equal(MapEventCreationOutcome.Rejected, reply.Outcome);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(context.SettlementId, out var settlement));
            Assert.Null(settlement.Party.MapEvent);
        });
    }

    [Fact]
    public void RelieveOwnCastle_DuringWallAssault_ContinuesTheDefendersEncounter()
    {
        var client = Clients.First();
        var castleId = CreateCastleAssault(client, playerIsBesieger: false);
        var router = client.Resolve<TestNetworkRouter>();
        router.SetLatency(client.NetPeer, Server.NetPeer, TimeSpan.FromMilliseconds(50));

        client.Call(() =>
        {
            var castle = client.GetRegisteredObject<Settlement>(castleId);
            Assert.NotNull(castle.OwnerClan);
            Assert.Same(Hero.MainHero.Clan, castle.OwnerClan);
            Assert.Null(MobileParty.MainParty.MapEvent);
            PlayerEncounter.Start();
            PlayerEncounter.Current.SetupFields(PartyBase.MainParty, castle.Party);
            // The menu opens before the server attaches the outside defender and changes Siege to SiegeOutside.
            AssertMenuAfter(client, "encounter", () =>
                new EncounterGameMenuBehavior().game_menu_join_encounter_help_defenders_on_consequence(
                    new MenuCallbackArgs(Campaign.Current.CurrentMenuContext, null)));
            Assert.Same(castle.Party.MapEvent, PlayerEncounter.Battle);
            Assert.True(PlayerEncounter.Battle.IsSiegeAssault);
            Assert.Equal(BattleSideEnum.Defender, PlayerEncounter.Current.PlayerSide);
        }, new[] { AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.CalculateStrengthOfParties)) });

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());
        Assert.Equal(BattleSideEnum.Defender, request.Side);
        Server.Call(() => router.RunUntilIdle(), MapEventDisabledMethods);
        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>()).Accepted);
        client.Call(() =>
        {
            var battle = client.GetRegisteredObject<MapEvent>(request.MapEventId);
            Assert.Same(battle.DefenderSide, PartyBase.MainParty.MapEventSide);
            PlayerEncounter.Update();
            Assert.Same(battle, PlayerEncounter.Battle);
            Assert.Equal(PlayerEncounterState.Wait, PlayerEncounter.Current.EncounterState);
            Assert.False(battle.HasWinner);
        });
    }

    [Fact]
    public void CaptureCastle_AfterWinningWallAssault_OpensAftermathForBesieger()
    {
        var client = Clients.First();
        var castleId = CreateCastleAssault(client, playerIsBesieger: true);
        client.Call(() =>
        {
            var castle = client.GetRegisteredObject<Settlement>(castleId);
            Assert.NotSame(Hero.MainHero.Clan, castle.OwnerClan);
            PlayerEncounter.Init();
            Assert.Same(PartyBase.MainParty, PlayerEncounter.Battle.AttackerSide.LeaderParty);
            Assert.Equal(BattleSideEnum.Attacker, PlayerEncounter.Current.PlayerSide);
        });

        // Deliver the victory and ownership while the client still has its pre-mission encounter.
        using (new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(MapEvent), nameof(MapEvent.OnBattleWon))))
        {
            Server.Call(() =>
            {
                var castle = Server.GetRegisteredObject<Settlement>(castleId);
                var battle = castle.Party.MapEvent;
                battle.BattleState = BattleState.AttackerVictory;
                var besiegerClan = battle.AttackerSide.LeaderParty.MobileParty.ActualClan;
                Assert.True(Server.ObjectManager.TryGetId(castle.Town, out var townId));
                Assert.True(Server.ObjectManager.TryGetId(besiegerClan, out var clanId));
                TestEnvironment.AssertReferenceField<Town, Clan>(nameof(Town._ownerClan), townId, clanId, castle.OwnerClan);
            });
        }

        client.Call(() =>
        {
            Assert.Equal(BattleSideEnum.Attacker, PlayerEncounter.Battle.WinningSide);
            Assert.Same(Hero.MainHero.Clan, PlayerEncounter.EncounterSettlement.OwnerClan);
            AssertMenuAfter(client, "menu_settlement_taken", () => GameMenu.SwitchToMenu("encounter"));
        });
    }

    private string CreateCastleAssault(EnvironmentInstance client, bool playerIsBesieger)
    {
        var castleId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var ownerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var besiegerId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var playerPartyId = playerIsBesieger ? besiegerId : ownerPartyId;
        TestEnvironment.AssertField<Town, bool>(nameof(Town._isCastle), true, townId);

        // The fixture supplies the two side leaders and has no world data for fortification bonuses.
        var disabled = MapEventDisabledMethods.Concat(SiegeCreationDisabledMethods)
            .Append(AccessTools.Method(typeof(DefaultCombatSimulationModel), nameof(DefaultCombatSimulationModel.GetSettlementAdvantage)))
            .Append(AccessTools.Method(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent.AddMapEventParties)))
            .Append(AccessTools.Method(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent.AddInsideSettlementParties)));
        Server.Call(() =>
        {
            var castle = Server.GetRegisteredObject<Settlement>(castleId);
            var owner = Server.GetRegisteredObject<MobileParty>(ownerPartyId);
            var besieger = Server.GetRegisteredObject<MobileParty>(besiegerId);
            CreateReplicableSiegeEvent(castle, besieger);
            castle.SetSettlementComponent(Server.GetRegisteredObject<Town>(townId));
            Assert.True(Server.ObjectManager.TryGetId(owner.ActualClan, out var ownerClanId));
            TestEnvironment.AssertReferenceField<Town, Clan>(nameof(Town._ownerClan), townId, ownerClanId);
            VillageHostileFactionStanceHelper.ApplyWarStance(owner.MapFaction, besieger.MapFaction);
            SiegeAssaultEventComponent.CreateSiegeAssaultMapEvent(besieger.Party, castle.Party);
        }, disabled);

        var playerParty = client.GetRegisteredObject<MobileParty>(playerPartyId);
        client.Call(() =>
        {
            Campaign.Current.MainParty = playerParty;
            Game.Current.PlayerTroop = playerParty.LeaderHero.CharacterObject;
            client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
            var castle = client.GetRegisteredObject<Settlement>(castleId);
            Assert.True(castle.IsCastle);
            Assert.True(castle.Party.MapEvent.IsSiegeAssault);
            Assert.Null(playerParty.CurrentSettlement);

            Campaign.Current.MapStateData ??= new MapStateData();
            var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
            starter.AddGameMenu("encounter", "Encounter", new EncounterGameMenuBehavior().game_menu_encounter_on_init);
            starter.AddGameMenu("menu_settlement_taken", "Settlement taken", _ => { });
            var state = Game.Current.GameStateManager.CreateState<MapState>();
            state._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
            state._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
            state._menuContext.GameMenu.StringId = playerIsBesieger ? "encounter" : "join_siege_event";
            Game.Current.GameStateManager._gameStates.Add(state);
        });
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(playerPartyId);
                Assert.True(instance.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
                Assert.True(instance.ObjectManager.TryGetId(party.ActualClan, out var clanId));
                Assert.True(instance.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out var characterId));
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(
                    new Player(ControllerId, heroId, playerPartyId, clanId, characterId)));
            });
        }
        TestEnvironment.ConnectRegisteredPlayer(client, ControllerId);
        return castleId;
    }

    private static void AssertMenuAfter(EnvironmentInstance client, string expectedMenu, Action openMenu)
    {
        var castle = PlayerEncounter.EncounterSettlement;
        // Skip the castle entry and vanilla encounter UI, but let the encounter behavior redirect the menu.
        using var engineCalls = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)),
            AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyForParty)),
            AccessTools.Method(typeof(EncounterManager), nameof(EncounterManager.StartSettlementEncounter)),
            AccessTools.Method(typeof(DefaultEncounter), nameof(DefaultEncounter.game_menu_encounter_on_init)));
        using var menus = new MethodCallRecorder(Priority.First, runOriginal: true,
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)));
        try
        {
            openMenu();
            Assert.Equal(expectedMenu, menus.MenusFor(client).LastOrDefault());
        }
        finally
        {
            client.Resolve<ISiegeEventInterface>().ReleaseLocalAftermathChoice(castle);
        }
    }

    private SiegeContext CreateContext(EnvironmentInstance client, OwnerStance stance)
    {
        var (_, partyId) = CreatePlayerHeroParty(ControllerId);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var campLeaderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetMapEventCreationTimeout(client, TimeSpan.FromMilliseconds(1));
        client.NetworkSentMessages.Clear();

        return new SiegeContext(partyId, settlementId, townId, ownerClanId, campLeaderId, stance);
    }

    // The settlement encounter that opens the join_siege_event menu, then its "Assault the siege camp" option.
    private static PlayerEncounter ChooseAssaultSiegeCamp(EnvironmentInstance client, SiegeContext context)
    {
        var encounter = StageSettlementEncounter(client, context);

        new EncounterGameMenuBehavior().game_menu_join_encounter_help_defenders_on_consequence(
            new MenuCallbackArgs((MenuContext)null, null));

        // The restart onto the camp waits for the server, so the encounter still targets the settlement.
        Assert.Same(encounter, PlayerEncounter.Current);
        Assert.True(encounter._defenderParty.IsSettlement);
        return encounter;
    }

    private static PlayerEncounter StageSettlementEncounter(EnvironmentInstance client, SiegeContext context)
    {
        var settlement = StageSiege(client, context);
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.PartyId, out var party));

        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._attackerParty = party.Party;
        encounter._defenderParty = settlement.Party;
        encounter._encounteredParty = settlement.Party;
        encounter.EncounterSettlementAux = settlement;
        Campaign.Current.PlayerEncounter = encounter;
        return encounter;
    }

    private static Settlement StageSiege(EnvironmentInstance instance, SiegeContext context)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(context.PartyId, out var party));
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(context.SettlementId, out var settlement));
        Assert.True(instance.ObjectManager.TryGetObject<Town>(context.TownId, out var town));
        Assert.True(instance.ObjectManager.TryGetObject<Clan>(context.OwnerClanId, out var ownerClan));
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(context.CampLeaderId, out var campLeader));
        var owner = context.Stance == OwnerStance.OwnClan ? party.ActualClan : ownerClan;

        using (new AllowedThread())
        {
            Campaign.Current.MainParty = party;
            settlement.SetSettlementComponent(town);
            if (context.Stance == OwnerStance.Hostile)
                VillageHostileFactionStanceHelper.ApplyWarStance(party.ActualClan, ownerClan);

            // An AI party besieging the town. Ownerless skips the constructor's relation change with the
            // owner clan leader, which these synthetic clans do not have.
            town.OwnerClan = null;
            var siegeEvent = new SiegeEvent(settlement, campLeader);
            town.OwnerClan = owner;
            siegeEvent.BesiegerCamp._besiegerParties.Add(campLeader);
            siegeEvent.BesiegerCamp._leaderParty = campLeader;
        }

        Assert.True(settlement.IsFortification);
        Assert.Same(campLeader, settlement.SiegeEvent.BesiegerCamp.LeaderParty);
        Assert.Equal(
            context.Stance == OwnerStance.Hostile,
            party.MapFaction.IsAtWarWith(settlement.MapFaction));
        return settlement;
    }

    private static void SetMapEventCreationTimeout(EnvironmentInstance instance, TimeSpan timeout)
    {
        instance.Call(() =>
        {
            var config = new Mock<INetworkConfig>();
            config.SetupGet(x => x.ObjectCreationTimeout).Returns(timeout);

            var coordinator = instance.Resolve<MapEventCreationCoordinator>();
            AccessTools.Field(typeof(MapEventCreationCoordinator), "configuration").SetValue(coordinator, config.Object);
        });
    }

    private enum OwnerStance
    {
        OwnClan,
        Neutral,
        Hostile,
    }

    private sealed record SiegeContext(
        string PartyId,
        string SettlementId,
        string TownId,
        string OwnerClanId,
        string CampLeaderId,
        OwnerStance Stance);
}
