using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Participation;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for issue #2939.</summary>
public class RetreatedPartyOutcomeTests : MissionTestEnvironment
{
    private const string EnemyController = "enemy";
    private const string RemainingController = "remaining";
    private const string RetreaterController = "retreater";

    public RetreatedPartyOutcomeTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void AlliedPlayerRetreats_AllyLaterLoses_RetreaterIsNotCaptured()
    {
        var setup = SetupBattle();
        EnterBattle(Clients.ElementAt(1), setup.MapEventId);
        EnterBattle(Clients.ElementAt(2), setup.MapEventId);
        DepartBattle(RetreaterController, setup.MapEventId, wasRetreat: true);

        CommitEnemyVictory(setup.MapEventId);

        AssertNotCaptive(Server, setup.RetreaterHeroId);
        AssertCaptiveOf(Server, setup.RemainingHeroId, setup.EnemyPartyId);
    }

    [Fact]
    public void AlliedPlayerRetreatsThenReenters_AllyLaterLoses_ReenteredPlayerIsCaptured()
    {
        var setup = SetupBattle();
        EnterBattle(Clients.ElementAt(1), setup.MapEventId);
        EnterBattle(Clients.ElementAt(2), setup.MapEventId);
        DepartBattle(RetreaterController, setup.MapEventId, wasRetreat: true);
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this,
            new MissionMemberEntered(RetreaterController, setup.MapEventId, isFirstMember: false)));

        CommitEnemyVictory(setup.MapEventId);

        AssertCaptiveOf(Server, setup.RetreaterHeroId, setup.EnemyPartyId);
        AssertCaptiveOf(Server, setup.RemainingHeroId, setup.EnemyPartyId);
    }

    [Fact]
    public void NetworkBattleRetreated_UsesAuthenticatedSendingPeerParty()
    {
        var (mapEventId, partyIds) = SetupCoopBattle(EnemyController, RemainingController, RetreaterController);
        var authenticatedRetreater = Clients.ElementAt(2);

        Server.Call(() => Server.Resolve<GameInterface.Services.Players.IPlayerManager>()
            .SetPeer(RetreaterController, authenticatedRetreater.NetPeer));
        authenticatedRetreater.Call(() => authenticatedRetreater.Resolve<Common.Network.INetwork>()
            .SendAll(new NetworkBattleRetreated(mapEventId)));

        AssertRetreated(mapEventId, partyIds[2], expected: true);
        AssertRetreated(mapEventId, partyIds[1], expected: false);
    }

    [Fact]
    public void NormalMissionLeave_DoesNotMarkPartyRetreated()
    {
        var (mapEventId, partyIds) = SetupCoopBattle(EnemyController, RemainingController, RetreaterController);

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this,
            new MissionMemberDeparted(RetreaterController, mapEventId, wasRetreat: true, isInstanceEmpty: false)));

        AssertRetreated(mapEventId, partyIds[2], expected: false);
    }
    
    [Fact]
    public void AlliedPlayerRetreats_AllyLaterWins_RemainingWinnerReceivesRewardsAndRetreaterDoesNot()
    {
        var setup = SetupBattle();
        EnterBattle(Clients.ElementAt(1), setup.MapEventId);
        EnterBattle(Clients.ElementAt(2), setup.MapEventId);
        DepartBattle(RetreaterController, setup.MapEventId, wasRetreat: true);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(setup.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(setup.RemainingPartyId, out var remainingParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(setup.RetreaterPartyId, out var retreaterParty));

            MapEventParty remainingMapEventParty = mapEvent.DefenderSide.Parties.Single(party => ReferenceEquals(party.Party, remainingParty.Party));
            MapEventParty retreaterMapEventParty = mapEvent.DefenderSide.Parties.Single(party => ReferenceEquals(party.Party, retreaterParty.Party));

            mapEvent.BattleState = BattleState.DefenderVictory;
            mapEvent.CalculateMapEventResults();

            Assert.True(remainingMapEventParty.GainedRenown > 0f);
            Assert.True(remainingMapEventParty.GainedInfluence > 0f);
            Assert.Equal(0f, retreaterMapEventParty.GainedRenown);
            Assert.Equal(0f, retreaterMapEventParty.GainedInfluence);

            float remainingRenownBefore = remainingParty.LeaderHero.Clan.Renown;
            float remainingInfluenceBefore = remainingParty.LeaderHero.Clan.Influence;
            float retreaterRenownBefore = retreaterParty.LeaderHero.Clan.Renown;
            float retreaterInfluenceBefore = retreaterParty.LeaderHero.Clan.Influence;
            var tracker = Server.Resolve<IRetreatedMapEventPartyTracker>();

            MapEventPatches.CommitCalculatedMapEventResults(mapEvent, party => !tracker.IsRetreated(mapEvent, party.Party));

            Assert.True(remainingParty.LeaderHero.Clan.Renown > remainingRenownBefore);
            Assert.True(remainingParty.LeaderHero.Clan.Influence > remainingInfluenceBefore);
            Assert.Equal(retreaterRenownBefore, retreaterParty.LeaderHero.Clan.Renown);
            Assert.Equal(retreaterInfluenceBefore, retreaterParty.LeaderHero.Clan.Influence);
        }, MapEventDisabledMethods);
    }

    private OutcomeSetup SetupBattle()
    {
        var (mapEventId, partyIds, heroIds) = SetupCoopBattleWithHeroes(EnemyController, RemainingController, RetreaterController);
        SeedHeroInParty(heroIds[1], partyIds[1]);
        SeedHeroInParty(heroIds[2], partyIds[2]);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var remainingParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[2], out var retreaterParty));

            retreaterParty.Party.MapEventSide = mapEvent.DefenderSide;

            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            remainingParty.LeaderHero.Clan.Kingdom = kingdom;
            retreaterParty.LeaderHero.Clan.Kingdom = kingdom;

            mapEvent.RecalculateStrengthOfSides();

            foreach (MapEventSide side in mapEvent._sides)
            {
                side?.CalculateRenownAndInfluenceValuesOnPartyInvolved(mapEvent.StrengthOfSide);
            }
        }, MapEventDisabledMethods);

        return new OutcomeSetup(mapEventId, partyIds[0], partyIds[1], partyIds[2], heroIds[2], heroIds[1]);
    }

    private void SeedHeroInParty(string heroId, string partyId)
    {
        void Seed(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                using (new AllowedThread())
                {
                    party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                    hero.PartyBelongedTo = party;
                }
            }, MapEventDisabledMethods);
        }

        Seed(Server);
        foreach (var client in Clients) Seed(client);
    }

    private void CommitEnemyVictory(string mapEventId)
    {
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize")).ToList();

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new AuthoritativeBattleConclusionRequested(mapEventId, BattleState.AttackerVictory, hostEpoch: 1)), disabledMethods);
    }

    private void AssertRetreated(string mapEventId, string partyId, bool expected)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Equal(expected,
                Server.Resolve<IRetreatedMapEventPartyTracker>().IsRetreated(mapEvent, party.Party));
        });
    }

    private static void AssertNotCaptive(EnvironmentInstance instance, string heroId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.False(hero.IsPrisoner);
            Assert.Null(hero.PartyBelongedToAsPrisoner);
        });
    }

    private static void AssertCaptiveOf(EnvironmentInstance instance, string heroId, string captorPartyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));
            Assert.True(hero.IsPrisoner);
            Assert.Same(captor.Party, hero.PartyBelongedToAsPrisoner);
        });
    }

    private readonly struct OutcomeSetup
    {
        public string MapEventId { get; }
        public string EnemyPartyId { get; }
        public string RemainingPartyId { get; }
        public string RetreaterPartyId { get; }
        public string RetreaterHeroId { get; }
        public string RemainingHeroId { get; }

        public OutcomeSetup(string mapEventId, string enemyPartyId, string remainingPartyId, string retreaterPartyId, string retreaterHeroId, string remainingHeroId)
        {
            MapEventId = mapEventId;
            EnemyPartyId = enemyPartyId;
            RemainingPartyId = remainingPartyId;
            RetreaterPartyId = retreaterPartyId;
            RetreaterHeroId = retreaterHeroId;
            RemainingHeroId = remainingHeroId;
        }
    }
}