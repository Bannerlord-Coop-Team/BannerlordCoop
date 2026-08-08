using Common.Messaging;
using Common.Util;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Util;
using GameInterface.Services.Barters;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using GameInterface.Services.Villages.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class HideoutMapEventTests : MapEventTestBase
{
    public HideoutMapEventTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PlayerStartsHideoutBattle_BanditOccupantJoinsWithSinglePlayerInitializationBroadcast()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("hideout-attacker");
        string? attackerMapEventPartyId = null;
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            settlement.SetSettlementComponent(hideout);

            var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
            banditClan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            var banditParty = BanditPartyComponent.CreateBanditParty(
                "E2EHideoutDefender",
                banditClan,
                hideout,
                isBossParty: false,
                pt: null,
                new CampaignVec2(Vec2.Zero, true));

            banditParty.CurrentSettlement = settlement;
            VillageHostileFactionStanceHelper.ApplyWarStance(playerParty.MapFaction, banditParty.MapFaction);

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            var broker = Server.Resolve<IMessageBroker>();
            var initializationBroadcasts = 0;
            var playerPartyWasBroadcast = false;
            void CountInitializationBroadcast(MessagePayload<MapEventInvolvedPartiesAdded> payload)
            {
                if (!ReferenceEquals(payload.What.MapEvent, mapEvent))
                    return;

                initializationBroadcasts++;
                playerPartyWasBroadcast |= payload.What.AddedParties.Any(
                    party => party.Party == playerParty.Party);
            }

            broker.Subscribe<MapEventInvolvedPartiesAdded>(CountInitializationBroadcast);
            try
            {
                mapEvent.Initialize(
                    playerParty.Party,
                    settlement.Party,
                    new HideoutEventComponent(mapEvent, isSendTroops: false),
                    MapEvent.BattleTypes.Hideout);
            }
            finally
            {
                broker.Unsubscribe<MapEventInvolvedPartiesAdded>(CountInitializationBroadcast);
            }
            mapEvent.MapEventVisual = null;

            Assert.Same(settlement, banditParty.CurrentSettlement);
            Assert.Same(mapEvent.DefenderSide, banditParty.Party.MapEventSide);
            Assert.Equal(1, initializationBroadcasts);
            Assert.True(playerPartyWasBroadcast);
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            var attackerMapEventParty = Assert.Single(
                mapEvent.AttackerSide.Parties,
                party => party.Party == playerParty.Party);
            Assert.True(Server.ObjectManager.TryGetId(attackerMapEventParty, out attackerMapEventPartyId));
        }, MapEventDisabledMethods);

        var involvedParties = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkAddInvolvedParties>(),
            message => message.MapEventId == mapEventId);
        Assert.Contains(attackerMapEventPartyId, involvedParties.MapEventPartyIds);
        var attackerRoster = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkUpdateMapEventParty>(),
            message => message.MapEventPartyId == attackerMapEventPartyId);
        Assert.NotEmpty(attackerRoster.FlattenedTroops);
    }

    [Fact]
    public void HourlyTickSettlement_InactivePlayerPartyDoesNotSpotHideout()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("inactive-hideout-spotter");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            playerParty.IsActive = false;
            playerParty.Position = new CampaignVec2(Vec2.Zero, true);

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement._position = new CampaignVec2(Vec2.Zero, true);
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            settlement.SetSettlementComponent(hideout);

            var requiredBanditParties = System.Math.Max(
                1,
                Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt);
            for (var i = 0; i < requiredBanditParties; i++)
            {
                var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
                banditClan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
                var banditParty = BanditPartyComponent.CreateBanditParty(
                    $"E2EInactiveSpotterBandit{i}",
                    banditClan,
                    hideout,
                    isBossParty: false,
                    pt: null,
                    new CampaignVec2(Vec2.Zero, true));
                banditParty.CurrentSettlement = settlement;
            }

            Assert.True(hideout.IsInfested);

            var behavior = new HideoutCampaignBehavior();
            behavior.HourlyTickSettlement(settlement);
            Assert.False(hideout.IsSpotted);

            playerParty.IsActive = true;
            behavior.HourlyTickSettlement(settlement);
            Assert.True(hideout.IsSpotted);
            Assert.True(settlement.IsVisible);
        });
    }

    [Fact]
    public void ConsequenceRequests_ApplyMissionPreparationCooldownAndClearRewardsOnServer()
    {
        const string controllerId = "hideout-consequences";
        var (playerHeroId, playerPartyId) = CreatePlayerHeroParty(controllerId);
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);

        string? notableId = null;
        string? settlementId = null;
        string? banditTroopId = null;
        var banditParties = new List<(string PartyId, int TroopCount)>();
        var initialBanditCount = 0;
        var maximumMissionBandits = 0;
        var expectedNotableRelation = 0;
        var expectedNextAttackTime = CampaignTime.Zero;

        Server.Call(() =>
        {
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[]
            {
                new HideoutCampaignBehavior(),
            }));

            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            playerParty.IsActive = true;

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement._position = new CampaignVec2(Vec2.Zero, true);
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            settlement.SetSettlementComponent(hideout);
            playerParty.CurrentSettlement = settlement;
            hideout._nextPossibleAttackTime = new CampaignTime(-1);

            var model = Campaign.Current.Models.BanditDensityModel;
            maximumMissionBandits =
                model.NumberOfMaximumTroopCountForFirstFightInHideout +
                model.NumberOfMaximumTroopCountForBossFightInHideout;
            var requiredBanditParties = System.Math.Max(
                1,
                model.NumberOfMinimumBanditPartiesInAHideoutToInfestIt);
            var banditTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            Assert.True(Server.ObjectManager.TryGetId(banditTroop, out banditTroopId));

            for (var i = 0; i < requiredBanditParties; i++)
            {
                var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
                banditClan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
                var banditParty = BanditPartyComponent.CreateBanditParty(
                    $"E2EHideoutConsequenceBandit{i}",
                    banditClan,
                    hideout,
                    isBossParty: false,
                    pt: null,
                    new CampaignVec2(Vec2.Zero, true));
                banditParty.CurrentSettlement = settlement;
                var troopCount = i == 0 ? maximumMissionBandits + 5 : 1;
                banditParty.MemberRoster.AddToCounts(banditTroop, troopCount);
                Assert.True(Server.ObjectManager.TryGetId(banditParty, out var banditPartyId));
                banditParties.Add((banditPartyId, troopCount));
            }

            Assert.True(hideout.IsInfested);
            Assert.True(hideout.NextPossibleAttackTime.IsPast);
            initialBanditCount = settlement.Parties
                .Where(party => party.IsBandit)
                .Sum(party => party.MemberRoster.TotalHealthyCount);
            expectedNextAttackTime = CampaignTime.Now + Campaign.Current.Models.HideoutModel.HideoutHiddenDuration;
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));

            var villageSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            villageSettlement._position = new CampaignVec2(Vec2.Zero, true);
            villageSettlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Village>());
            Campaign.Current._villages.Add(villageSettlement.Village);
            var notable = GameObjectCreator.CreateInitializedObject<Hero>();
            notable.Occupation = Occupation.Artisan;
            villageSettlement.AddHeroWithoutParty(notable);
            Assert.True(notable.IsNotable);
            Assert.Contains(notable, villageSettlement.Notables);
            Assert.Contains(villageSettlement.Village, Campaign.Current.AllVillages);
            Assert.True(Server.ObjectManager.TryGetId(notable, out notableId));
        });

        TestEnvironment.FlushCoalescer();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
            Assert.Same(settlement, playerParty.CurrentSettlement);
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(banditTroopId!, out var banditTroop));

            foreach (var (banditPartyId, troopCount) in banditParties)
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var banditParty));
                banditParty.SetCurrentSettlementDirectly(settlement);
                if (!settlement._partiesCache.Contains(banditParty))
                    settlement._partiesCache.Add(banditParty);
                Assert.Equal(troopCount, banditParty.MemberRoster.GetTroopCount(banditTroop));
            }

            int GetBanditCount() => settlement.Parties
                .Where(party => party.IsBandit)
                .Sum(party => party.MemberRoster.TotalHealthyCount);

            Assert.True(initialBanditCount > maximumMissionBandits);
            Assert.Equal(initialBanditCount, GetBanditCount());

            using (new BarterPlayerContext(playerHero, playerParty))
                new HideoutCampaignBehavior().ArrangeHideoutTroopCountsForMission();

            Assert.Equal(initialBanditCount, GetBanditCount());
        });

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            client.NetPeer,
            new NetworkHideoutCampaignConsequenceRequested(
                settlementId!,
                HideoutCampaignConsequence.SetAttackCooldown)));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
            Assert.Equal(expectedNextAttackTime, settlement.Hideout.NextPossibleAttackTime);
            Assert.Equal(
                initialBanditCount,
                settlement.Parties.Where(party => party.IsBandit).Sum(party => party.MemberRoster.TotalHealthyCount));
            settlement.Hideout._nextPossibleAttackTime = new CampaignTime(-1);
        });

        Server.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
            using (new BarterPlayerContext(playerHero, playerParty))
                new HideoutCampaignBehavior().OnTroopRosterManageDone(null, isDirectAssault: false);
            Assert.Equal(
                maximumMissionBandits,
                settlement.Parties.Where(party => party.IsBandit)
                    .Sum(party => party.MemberRoster.TotalHealthyCount));
        }, new[] { AccessTools.Method(typeof(HideoutCampaignBehavior), "OnTroopRosterManageDone") });

        var preparationMessages = Server.NetworkSentMessages.Messages;
        var preparationReplyIndex = preparationMessages.FindIndex(
            message => message is NetworkHideoutCampaignConsequenceResolved);
        var finalRosterDeltaIndex = preparationMessages.FindLastIndex(
            message => message is NetworkTroopRosterElementBatch);
        Assert.True(finalRosterDeltaIndex >= 0);
        Assert.True(finalRosterDeltaIndex < preparationReplyIndex);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
            Assert.Equal(expectedNextAttackTime, settlement.Hideout.NextPossibleAttackTime);
            Assert.Equal(
                maximumMissionBandits,
                settlement.Parties.Where(party => party.IsBandit).Sum(party => party.MemberRoster.TotalHealthyCount));

            foreach (var banditParty in settlement.Parties.Where(party => party.IsBandit).ToList())
                banditParty.CurrentSettlement = null;

            Assert.False(settlement.Hideout.IsInfested);
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(client.NetPeer, out var registeredPlayer));
            Assert.Equal(playerHeroId, registeredPlayer.HeroId);
            Assert.Equal(playerPartyId, registeredPlayer.MobilePartyId);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.Same(settlement, playerParty.CurrentSettlement);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId!, out var notable));
            expectedNotableRelation = playerHero.GetRelation(notable) + 2;

            Server.Resolve<IMessageBroker>().Publish(
                client.NetPeer,
                new NetworkHideoutCampaignConsequenceRequested(
                    settlementId!,
                    HideoutCampaignConsequence.GrantClearRewards));
        });

        foreach (var connectedClient in Clients)
        {
            connectedClient.Call(() =>
            {
                Assert.True(connectedClient.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
                Assert.True(connectedClient.ObjectManager.TryGetObject<Hero>(notableId!, out var notable));
                Assert.Equal(expectedNotableRelation, playerHero.GetRelation(notable));
            });
        }
    }

    [Fact]
    public void DirectAssaultPreparation_ReachesTwentyFiveDefendersBeforeReplying()
    {
        const string controllerId = "hideout-direct-assault";
        var (playerHeroId, playerPartyId) = CreatePlayerHeroParty(controllerId);
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);

        string? settlementId = null;
        string? banditTroopId = null;
        var banditParties = new List<string>();

        Server.Call(() =>
        {
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[]
            {
                new HideoutCampaignBehavior(),
            }));

            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            playerParty.IsActive = true;

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement._position = new CampaignVec2(Vec2.Zero, true);
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            settlement.SetSettlementComponent(hideout);
            playerParty.CurrentSettlement = settlement;
            hideout._nextPossibleAttackTime = new CampaignTime(-1);

            var banditCulture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            var banditTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            banditTroop.Culture = banditCulture;
            banditCulture.BanditBandit = banditTroop;
            Assert.True(Server.ObjectManager.TryGetId(banditTroop, out banditTroopId));

            var requiredBanditParties = System.Math.Max(
                1,
                Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt);
            for (var i = 0; i < requiredBanditParties; i++)
            {
                var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
                banditClan.Culture = banditCulture;
                var banditParty = BanditPartyComponent.CreateBanditParty(
                    $"E2EDirectAssaultBandit{i}",
                    banditClan,
                    hideout,
                    isBossParty: false,
                    pt: null,
                    new CampaignVec2(Vec2.Zero, true));
                banditParty.CurrentSettlement = settlement;
                banditParty.MemberRoster.AddToCounts(banditTroop, 1);
                Assert.True(Server.ObjectManager.TryGetId(banditParty, out var banditPartyId));
                banditParties.Add(banditPartyId);
            }

            Assert.True(hideout.IsInfested);
            Assert.True(hideout.NextPossibleAttackTime.IsPast);
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
        });

        TestEnvironment.FlushCoalescer();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(banditTroopId!, out var banditTroop));

            foreach (var banditPartyId in banditParties)
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var banditParty));
                banditParty.SetCurrentSettlementDirectly(settlement);
                if (!settlement._partiesCache.Contains(banditParty))
                    settlement._partiesCache.Add(banditParty);
                Assert.Equal(1, banditParty.MemberRoster.GetTroopCount(banditTroop));
            }

            Assert.True(
                settlement.Parties.Where(party => party.IsBandit)
                    .Sum(party => party.MemberRoster.TotalHealthyCount) < 25);
        });

        Server.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
            using (new BarterPlayerContext(playerHero, playerParty))
                new HideoutCampaignBehavior().OnTroopRosterManageDone(null, isDirectAssault: true);
            Assert.Equal(
                25,
                settlement.Parties.Where(party => party.IsBandit)
                    .Sum(party => party.MemberRoster.TotalHealthyCount));
        }, new[] { AccessTools.Method(typeof(HideoutCampaignBehavior), "OnTroopRosterManageDone") });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
            Assert.Equal(
                25,
                settlement.Parties.Where(party => party.IsBandit)
                    .Sum(party => party.MemberRoster.TotalHealthyCount));
        });

        var request = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkHideoutCampaignConsequenceRequested>(),
            message => message.Consequence == HideoutCampaignConsequence.PrepareDirectAssaultMission);
        var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHideoutCampaignConsequenceResolved>());
        Assert.Equal(request.RequestId, reply.RequestId);
        Assert.True(reply.Accepted);
        Assert.Equal(25, reply.ExpectedHealthyDefenderCount);

        var messages = Server.NetworkSentMessages.Messages;
        var replyIndex = messages.FindIndex(message => message is NetworkHideoutCampaignConsequenceResolved);
        var finalRosterDeltaIndex = messages.FindLastIndex(message => message is NetworkTroopRosterElementBatch);
        Assert.True(finalRosterDeltaIndex >= 0);
        Assert.True(finalRosterDeltaIndex < replyIndex);
    }

    [Fact]
    public void HideoutSendTroops_ClientReceivesSendTroopsMode()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("hideout-send-troops");
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Hideout>());

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(
                playerParty.Party,
                settlement.Party,
                new HideoutEventComponent(mapEvent, isSendTroops: true),
                MapEvent.BattleTypes.Hideout);
            mapEvent.MapEventVisual = null;

            if (!Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent))
                Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
                var component = Assert.IsType<HideoutEventComponent>(mapEvent.Component);
                Assert.True(component.IsSendTroops);
            }, MapEventDisabledMethods);
        }
    }

    [Fact]
    public void PlayerLeavesHideout_ServerClearsSettlementAndMapEvent()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("hideout-leaver");
        var requester = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(requester, "hideout-leaver");
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Hideout>());
            EnterSettlementAction.ApplyForParty(playerParty, settlement);

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(
                playerParty.Party,
                settlement.Party,
                new HideoutEventComponent(mapEvent, isSendTroops: false),
                MapEvent.BattleTypes.Hideout);
            mapEvent.MapEventVisual = null;

            if (!Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent))
                Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.Same(settlement, playerParty.CurrentSettlement);
            Assert.Same(mapEvent, playerParty.MapEvent);
        }, MapEventDisabledMethods);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(
                requester.NetPeer,
                new NetworkRequestEndSettlementEncounter(playerPartyId));
        }, MapEventDisabledMethods);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.Null(playerParty.CurrentSettlement);
            Assert.Null(playerParty.MapEvent);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out _));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void JoinedPlayerLeavesHideout_ServerRemovesOnlyJoinedParty()
    {
        var (_, leaderPartyId) = CreatePlayerHeroParty("hideout-leader");
        var (_, joinedPartyId) = CreatePlayerHeroParty("hideout-joiner");
        var requester = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(requester, "hideout-joiner");
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leaderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinedPartyId, out var joinedParty));

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Hideout>());
            EnterSettlementAction.ApplyForParty(leaderParty, settlement);
            EnterSettlementAction.ApplyForParty(joinedParty, settlement);

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(
                leaderParty.Party,
                settlement.Party,
                new HideoutEventComponent(mapEvent, isSendTroops: false),
                MapEvent.BattleTypes.Hideout);
            joinedParty.Party.MapEventSide = mapEvent.AttackerSide;
            mapEvent.MapEventVisual = null;

            if (!Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent))
                Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.Same(mapEvent, joinedParty.MapEvent);
        }, MapEventDisabledMethods);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(
                requester.NetPeer,
                new NetworkRequestEndSettlementEncounter(joinedPartyId));
        }, MapEventDisabledMethods);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leaderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinedPartyId, out var joinedParty));
            Assert.Null(joinedParty.CurrentSettlement);
            Assert.Null(joinedParty.MapEvent);
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
            Assert.Same(mapEvent, leaderParty.MapEvent);
        }, MapEventDisabledMethods);
    }
}
