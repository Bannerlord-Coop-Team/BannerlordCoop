using Common.Messaging;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Hideouts;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Hideouts;

public class HideoutRaidEntryTests : MapEventTestBase
{
    public HideoutRaidEntryTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(true, 14, 0, false)]
    [InlineData(true, 10, 4, false)]
    [InlineData(false, 39, 0, false)]
    [InlineData(false, 30, 9, false)]
    [InlineData(true, 14, 0, true)]
    public void FirstRaidCreation_LeavesWaitingPlayerInsideAndAdmitsOnlyRemainingSharedTroops(
        bool isDirectAssault, int firstEscortCount, int joiningEscortCount, bool abortFirstCreation)
    {
        var first = CreatePlayerHeroParty("hideout-entry-first");
        var joining = CreatePlayerHeroParty("hideout-entry-joining");
        var clients = Clients.ToArray();
        TestEnvironment.ConnectRegisteredPlayer(clients[0], "hideout-entry-first");
        TestEnvironment.ConnectRegisteredPlayer(clients[1], "hideout-entry-joining");
        string settlementId = null, firstCharacterId = null, joiningCharacterId = null, escortId = null;

        Server.Call(() =>
        {
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[]
            {
                new HideoutCampaignBehavior(),
            }));
            Campaign.Current.MapTimeTracker._numTicks = CampaignTime.Hours(isDirectAssault ? 12 : 0).NumTicks;
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement._position = new CampaignVec2(Vec2.Zero, true);
            settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Hideout>());
            settlement.Hideout._nextPossibleAttackTime = new CampaignTime(-1);
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));

            var escort = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            Assert.True(Server.ObjectManager.TryGetId(escort, out escortId));
            firstCharacterId = PrepareParticipant("hideout-entry-first", settlement, escort);
            joiningCharacterId = PrepareParticipant("hideout-entry-joining", settlement, escort);

            var banditCulture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            var bandit = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            bandit.Culture = banditCulture;
            banditCulture.BanditBandit = bandit;
            for (var index = 0; index < Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt; index++)
            {
                var clan = GameObjectCreator.CreateInitializedObject<Clan>();
                clan.Culture = banditCulture;
                var party = BanditPartyComponent.CreateBanditParty($"HideoutEntryBandit{index}", clan,
                    settlement.Hideout, false, null, new CampaignVec2(Vec2.Zero, true));
                party.CurrentSettlement = settlement;
                party.MemberRoster.AddToCounts(bandit, 1);
                foreach (var player in Server.Resolve<IPlayerManager>().Players)
                {
                    Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
                    VillageHostileFactionStanceHelper.ApplyWarStance(playerParty.MapFaction, party.MapFaction);
                }
            }
            Campaign.Current.MainParty = null;
        });

        Server.SimulateMessage(clients[1].NetPeer,
            new NetworkHideoutRaidEntryRequest("waiting-for-preparation", settlementId, isDirectAssault, true,
                Array.Empty<HideoutTroopSelectionEntry>()));
        var waitingReply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHideoutRaidEntryReply>(),
            reply => reply.RequestId == "waiting-for-preparation");
        Assert.False(waitingReply.Accepted);
        Assert.StartsWith("Waiting for ", waitingReply.Reason);

        var visualPatch = new Harmony($"HideoutRaidEntryTests.{Guid.NewGuid()}");
        visualPatch.Patch(AccessTools.PropertyGetter(typeof(CampaignTime), nameof(CampaignTime.Now)),
            postfix: new HarmonyMethod(typeof(HideoutRaidEntryTests), nameof(ProvideCampaignTime)));
        var initialize = AccessTools.Method(typeof(MapEvent), nameof(MapEvent.Initialize),
            new[] { typeof(PartyBase), typeof(PartyBase), typeof(MapEventComponent), typeof(MapEvent.BattleTypes) });
        visualPatch.Patch(initialize,
            prefix: new HarmonyMethod(typeof(HideoutRaidEntryTests), nameof(ProvideHeadlessVisual)),
            postfix: new HarmonyMethod(typeof(HideoutRaidEntryTests), nameof(ClearHeadlessVisual)));
        try
        {
            Server.SimulateMessage(clients[0].NetPeer,
                new NetworkHideoutRaidEntryRequest("duplicate-hero", settlementId, isDirectAssault, false,
                    Enumerable.Repeat(new HideoutTroopSelectionEntry(firstCharacterId, 1), isDirectAssault ? 8 : 25).ToArray()));
            Assert.False(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHideoutRaidEntryReply>(),
                reply => reply.RequestId == "duplicate-hero").Accepted);

            if (abortFirstCreation)
            {
                // An unregistered visual makes the real initialization barrier abort its graph.
                visualPatch.Unpatch(initialize, HarmonyPatchType.Postfix, visualPatch.Id);
                Server.Call(() => Server.Resolve<IMessageBroker>().Publish(clients[0].NetPeer,
                    new NetworkHideoutRaidEntryRequest("aborted-first", settlementId, isDirectAssault, false,
                        new[] { new HideoutTroopSelectionEntry(firstCharacterId, 1),
                            new HideoutTroopSelectionEntry(escortId, firstEscortCount) })), MapEventDisabledMethods);
                Assert.False(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHideoutRaidEntryReply>(),
                    reply => reply.RequestId == "aborted-first").Accepted);
                Server.Call(() =>
                {
                    Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                    Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(first.partyId, out var party));
                    Assert.Null(party.MapEvent);
                    Assert.True(settlement.Hideout.NextPossibleAttackTime.IsPast);
                    var selections = Server.Resolve<IHideoutTroopSelection>();
                    Assert.False(selections.HasSelection(settlement, party));
                    Assert.Equal(14, selections.GetRemaining(settlement, 14));
                    Assert.Null(Campaign.Current.MainParty);
                });
                visualPatch.Patch(initialize,
                    postfix: new HarmonyMethod(typeof(HideoutRaidEntryTests), nameof(ClearHeadlessVisual)));
            }

            Server.Call(() => Server.Resolve<IMessageBroker>().Publish(clients[0].NetPeer,
                new NetworkHideoutRaidEntryRequest("first", settlementId, isDirectAssault, false,
                    new[] { new HideoutTroopSelectionEntry(firstCharacterId, 1),
                        new HideoutTroopSelectionEntry(escortId, firstEscortCount) })), MapEventDisabledMethods);
        }
        finally
        {
            visualPatch.UnpatchAll(visualPatch.Id);
        }

        var firstReply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHideoutRaidEntryReply>(),
            reply => reply.RequestId == "first");
        Assert.True(firstReply.Accepted, firstReply.Reason);
        Assert.Equal(joiningEscortCount, firstReply.RemainingTroops);

        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(firstReply.MapEventId, out var mapEvent));
                mapEvent.Position = new CampaignVec2(Vec2.Zero, true);
            });

        Server.Call(() =>
        {
            Assert.Null(Campaign.Current.MainParty);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joining.partyId, out var waitingParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.Same(settlement, waitingParty.CurrentSettlement);
            Assert.Null(waitingParty.MapEvent);
            Assert.False(settlement.Hideout.NextPossibleAttackTime.IsPast);
            Assert.Equal(isDirectAssault ? 25 : 10, settlement.Parties.Where(party => party.IsBandit || party.IsBanditBossParty)
                .Sum(party => party.MemberRoster.TotalHealthyCount));
            Server.Resolve<BattleMissionStartHandler>().GetOrCreateMissionInitializerSnapshot(firstReply.MapEventId,
                () => new MissionInitializerRecord("test-hideout") { SceneLevels = isDirectAssault ? "level_2" : "level_1" });
        });

        var spawnSettings = new[]
        {
            AccessTools.PropertyGetter(typeof(MBGameManager), nameof(MBGameManager.UnitSpawnPrioritization)),
        };
        // The headless manager uses Default without loading the engine's BannerlordConfig.
        Server.Call(() => Server.SimulateMessage(clients[0].NetPeer, new NetworkBattleStartRequest("first-start",
            (int)BattleStartMode.Mission, firstReply.MapEventId, first.partyId)), spawnSettings);
        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>(),
            reply => reply.RequestId == "first-start").Accepted);
        var firstStart = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());
        Assert.True(firstStart.IsHideout);
        Assert.Equal(isDirectAssault, firstStart.IsDirectAssault);
        Assert.Equal(first.partyId, firstStart.InitiatingPartyId);
        Assert.Equal(firstReply.MapEventId, firstStart.MapEventId);
        TroopReserveEntry[] firstReserveEntries = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(firstReply.MapEventId, out var mapEvent));
            firstReserveEntries = Assert.Single(Server.Resolve<IBattleTroopReserveBuilder>()
                .GetOwnedReserves(mapEvent, "hideout-entry-first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties).Entries;
            Assert.Equal(firstEscortCount + 1, firstReserveEntries.Length);
        });

        Server.SimulateMessage(clients[1].NetPeer,
            new NetworkHideoutRaidEntryRequest("overfull", settlementId, isDirectAssault, false,
                new[] { new HideoutTroopSelectionEntry(joiningCharacterId, 1),
                    new HideoutTroopSelectionEntry(escortId, joiningEscortCount + 1) }));
        Assert.False(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHideoutRaidEntryReply>(),
            reply => reply.RequestId == "overfull").Accepted);

        var joinTroops = new List<HideoutTroopSelectionEntry> { new(joiningCharacterId, 1) };
        if (joiningEscortCount > 0) joinTroops.Add(new HideoutTroopSelectionEntry(escortId, joiningEscortCount));
        Server.SimulateMessage(clients[1].NetPeer,
            new NetworkHideoutRaidEntryRequest("joining", settlementId, !isDirectAssault, false, joinTroops.ToArray()));
        var joinReply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHideoutRaidEntryReply>(),
            reply => reply.RequestId == "joining");
        Assert.True(joinReply.Accepted, joinReply.Reason);
        Assert.True(joinReply.IsJoining);
        Assert.Equal(isDirectAssault, joinReply.IsDirectAssault);
        Assert.Equal(firstReply.MapEventId, joinReply.MapEventId);
        Assert.Equal(0, joinReply.RemainingTroops);

        Server.Call(() => Server.SimulateMessage(clients[1].NetPeer, new NetworkBattleStartRequest("joining-start",
            (int)BattleStartMode.Mission, firstReply.MapEventId, joining.partyId)), spawnSettings);
        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>(),
            reply => reply.RequestId == "joining-start").Accepted);
        Assert.All(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>(), start =>
        {
            Assert.True(start.IsHideout);
            Assert.Equal(isDirectAssault, start.IsDirectAssault);
            Assert.Equal(first.partyId, start.InitiatingPartyId);
            Assert.Equal(firstReply.MapEventId, start.MapEventId);
            Assert.Equal(firstStart.MissionInitializer.SceneLevels, start.MissionInitializer.SceneLevels);
        });
        Server.Call(() =>
        {
            Assert.Null(Campaign.Current.MainParty);
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(firstReply.MapEventId, out var mapEvent));
            var reserves = Server.Resolve<IBattleTroopReserveBuilder>();
            Assert.Equal(firstReserveEntries, Assert.Single(reserves.GetOwnedReserves(mapEvent, "hideout-entry-first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties).Entries);
            Assert.Equal(joiningEscortCount + 1, Assert.Single(reserves.GetOwnedReserves(mapEvent, "hideout-entry-joining", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties).Entries.Length);
        });

        foreach (var instance in Clients.Prepend(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(firstReply.MapEventId, out var mapEvent));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(first.partyId, out var firstParty));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(joining.partyId, out var joiningParty));
                Assert.Same(mapEvent.AttackerSide, firstParty.Party.MapEventSide);
                Assert.Same(mapEvent.AttackerSide, joiningParty.Party.MapEventSide);
                Assert.Contains(mapEvent, Campaign.Current.MapEventManager.MapEvents);
            });
    }

    private string PrepareParticipant(string controllerId, Settlement settlement, CharacterObject escort)
    {
        var players = Server.Resolve<IPlayerManager>();
        Assert.True(players.TryGetPlayer(controllerId, out var player));
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
        Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
        Assert.True(players.ReplacePlayer(player,
            new Player(player.ControllerId, player.HeroId, player.MobilePartyId, player.ClanId, characterId)));
        hero.HitPoints = 100;
        party.IsActive = true;
        party.Position = new CampaignVec2(Vec2.Zero, true);
        party.CurrentSettlement = settlement;
        party.MemberRoster.Clear();
        party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
        party.MemberRoster.AddToCounts(escort, 50);
        return characterId;
    }

    private static void ProvideHeadlessVisual(MapEvent __instance) => __instance.MapEventVisual = MockMapEventVisual();

    private static void ClearHeadlessVisual(MapEvent __instance) => __instance.MapEventVisual = null;

    private static void ProvideCampaignTime(ref CampaignTime __result) => __result = Campaign.Current.MapTimeTracker.Now;
}
