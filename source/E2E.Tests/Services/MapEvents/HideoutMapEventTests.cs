using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Util;
using GameInterface.Services.Villages.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
    public void PlayerStartsHideoutBattle_BanditOccupantRemainsInsideAndJoinsDefenders()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("hideout-attacker");

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
            mapEvent.Initialize(
                playerParty.Party,
                settlement.Party,
                new HideoutEventComponent(mapEvent, isSendTroops: false),
                MapEvent.BattleTypes.Hideout);
            mapEvent.MapEventVisual = null;

            Assert.Same(settlement, banditParty.CurrentSettlement);
            Assert.Same(mapEvent.DefenderSide, banditParty.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void PlayerLeavesHideout_ServerClearsSettlementAndMapEvent()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("hideout-leaver");
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

        var requester = Clients.First();
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

        var requester = Clients.First();
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
