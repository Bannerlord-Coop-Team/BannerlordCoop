using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

public class PartyDoneLogicPrisonerDonationTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.Single();

    public PartyDoneLogicPrisonerDonationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
    }

    [Fact]
    public void ValidDonation_TransfersPrisonerAndReplayDoesNotAwardInfluenceAgain()
    {
        var fixture = CreateFixture(sourcePrisonerCount: 1);
        var message = CreateDonationMessage(fixture);

        Send(message);

        float influenceAfterDonation = 0;
        Server.Call(() =>
        {
            var state = ResolveFixture(fixture);
            Assert.Equal(0, state.PlayerParty.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(1, state.Settlement.Party.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.True(state.PlayerParty.MobileParty.ActualClan.Influence > fixture.BaselineInfluence);
            influenceAfterDonation = state.PlayerParty.MobileParty.ActualClan.Influence;
        });

        Send(message);

        Server.Call(() =>
        {
            var state = ResolveFixture(fixture);
            Assert.Equal(0, state.PlayerParty.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(1, state.Settlement.Party.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(influenceAfterDonation, state.PlayerParty.MobileParty.ActualClan.Influence);
        });
    }

    [Fact]
    public void StaleDonation_ChangesNeitherRostersNorInfluence()
    {
        var fixture = CreateFixture(sourcePrisonerCount: 0);

        Send(CreateDonationMessage(fixture));

        Server.Call(() =>
        {
            var state = ResolveFixture(fixture);
            Assert.Equal(0, state.PlayerParty.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(0, state.Settlement.Party.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(fixture.BaselineInfluence, state.PlayerParty.MobileParty.ActualClan.Influence);
        });
    }

    [Fact]
    public void DonationAfterPlayerLeavesSettlement_ChangesNeitherRostersNorInfluence()
    {
        var fixture = CreateFixture(sourcePrisonerCount: 1);
        Server.Call(() => ResolveFixture(fixture).PlayerParty.MobileParty.CurrentSettlement = null);

        Send(CreateDonationMessage(fixture));

        Server.Call(() =>
        {
            var state = ResolveFixture(fixture);
            Assert.Equal(1, state.PlayerParty.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(0, state.Settlement.Party.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(fixture.BaselineInfluence, state.PlayerParty.MobileParty.ActualClan.Influence);
        });
    }

    [Fact]
    public void DonationWhenSettlementPrisonIsFull_ChangesNeitherRostersNorInfluence()
    {
        var fixture = CreateFixture(
            sourcePrisonerCount: 1,
            settlementPrisonerCount: 1,
            settlementPrisonerLimit: 1);

        Send(CreateDonationMessage(fixture));

        Server.Call(() =>
        {
            var state = ResolveFixture(fixture);
            Assert.Equal(1, state.PlayerParty.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(1, state.Settlement.Party.PrisonRoster.GetTroopCount(state.Prisoner));
            Assert.Equal(fixture.BaselineInfluence, state.PlayerParty.MobileParty.ActualClan.Influence);
        });
    }

    private DonationFixture CreateFixture(
        int sourcePrisonerCount,
        int settlementPrisonerCount = 0,
        int? settlementPrisonerLimit = null)
    {
        DonationFixture fixture = default;
        Server.Call(() =>
        {
            new InfluenceGainCampaignBehavior().RegisterEvents();

            var playerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            var settlementOwner = GameObjectCreator.CreateInitializedObject<Clan>();
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var prisoner = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            prisoner.Level = 20;
            playerParty.LeaderHero.Clan.Kingdom = kingdom;
            settlementOwner.Kingdom = kingdom;
            settlement.Town = town;
            settlement.SetSettlementComponent(town);
            town.OwnerClan = settlementOwner;
            town.IsOwnerUnassigned = false;
            playerParty.CurrentSettlement = settlement;

            if (sourcePrisonerCount != 0)
                playerParty.PrisonRoster.AddToCounts(prisoner, sourcePrisonerCount);
            if (settlementPrisonerCount != 0)
                settlement.Party.PrisonRoster.AddToCounts(prisoner, settlementPrisonerCount);
            if (settlementPrisonerLimit.HasValue)
            {
                settlement.Party._cachedPrisonerSizeLimit = settlementPrisonerLimit.Value;
                settlement.Party._prisonerSizeLastCheckVersion = settlement.Party.PrisonRoster.VersionNo;
            }

            Assert.True(Server.ObjectManager.TryGetId(playerParty.LeaderHero, out var mainHeroId));
            Assert.True(Server.ObjectManager.TryGetId(settlement, out var settlementId));
            Assert.True(Server.ObjectManager.TryGetId(prisoner, out var prisonerId));

            fixture = new DonationFixture(
                mainHeroId,
                settlementId,
                prisonerId,
                playerParty.LeaderHero.Clan.Influence);
        });
        TestEnvironment.FlushCoalescer();
        return fixture;
    }

    private static NetworkCompleteDoneLogic CreateDonationMessage(DonationFixture fixture)
    {
        var settlementDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData(fixture.PrisonerId, 1, 0, 0),
        });
        var playerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData(fixture.PrisonerId, -1, 0, 0),
        });
        var donatedPrisoners = new[]
        {
            new FlattenedTroop(
                fixture.PrisonerId,
                isHero: false,
                uniqueSeed: 1,
                RosterTroopState.Active,
                xp: 0,
                xpGained: 0),
        };

        return new NetworkCompleteDoneLogic(
            fixture.MainHeroId,
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            EmptyRosterData(),
            settlementDelta,
            EmptyRosterData(),
            playerDelta,
            Array.Empty<ItemRosterElement>(),
            new UpgradedTroopHistoryData(new List<UpgradedTroopHistoryElementData>()),
            leftPartyId: null,
            leftPrisonerRosterId: null,
            partyGoldChangeAmount: 0,
            partyInfluenceChangeAmount: 0,
            partyMoraleChangeAmount: 0,
            doNotApplyGoldTransactions: true,
            default,
            Helpers.PartyScreenHelper.PartyScreenMode.Normal,
            new TroopRosterOrderData(new()),
            applyReleasedAndTakenPrisonerActions: false,
            donationSettlementId: fixture.SettlementId,
            donatedPrisonersRoster: donatedPrisoners);
    }

    private void Send(NetworkCompleteDoneLogic message)
    {
        Client.Call(() => Client.Resolve<INetwork>().SendAll(message));
        TestEnvironment.FlushCoalescer();
    }

    private ResolvedDonationFixture ResolveFixture(DonationFixture fixture)
    {
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.MainHeroId, out var mainHero));
        Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
        Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(fixture.PrisonerId, out var prisoner));
        return new ResolvedDonationFixture(mainHero.PartyBelongedTo.Party, settlement, prisoner);
    }

    private static TroopRosterData EmptyRosterData() =>
        new(Array.Empty<TroopRosterElementData>());

    public void Dispose() => TestEnvironment.Dispose();

    private readonly record struct DonationFixture(
        string MainHeroId,
        string SettlementId,
        string PrisonerId,
        float BaselineInfluence);

    private readonly record struct ResolvedDonationFixture(
        PartyBase PlayerParty,
        Settlement Settlement,
        CharacterObject Prisoner);
}
