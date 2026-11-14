using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// Covers the garrison troop donation commit, including the empty donation that used to crash the dedicated server.
/// </summary>
public class GarrisonDonationTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.Single();

    public GarrisonDonationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
    }

    // A donation Done that lands after the party left the town keeps the troops in the party.
    [Fact]
    public void DonationAfterLeavingTheTown_KeepsTheTroops()
    {
        var fixture = CreateFixture(troopCount: 3);
        Server.Call(() => Resolve(fixture).Party.CurrentSettlement = null);

        Send(fixture, new[]
        {
            PartyScreenCommits.Transfer(
                PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, Server.GetHandle<CharacterObject>(fixture.TroopId), 3),
        });

        Server.Call(() =>
        {
            var state = Resolve(fixture);
            Assert.Null(state.Settlement.Town.GarrisonParty);
            Assert.Equal(3, state.Party.MemberRoster.GetTroopCount(state.Troop));
        });
    }

    // A Done with nothing moved arrives with null commands and must not create a garrison.
    [Fact]
    public void EmptyDonation_LeavesGarrisonUntouched()
    {
        var fixture = CreateFixture(troopCount: 3);

        Send(fixture, Array.Empty<PartyScreenCommandData>());

        Server.Call(() =>
        {
            var state = Resolve(fixture);
            Assert.Null(state.Settlement.Town.GarrisonParty);
            Assert.Equal(3, state.Party.MemberRoster.GetTroopCount(state.Troop));
        });
    }

    // A real donation creates the garrison and moves the troops into it on the server.
    [Fact]
    public void Donation_MovesTroopsToGarrison()
    {
        var fixture = CreateFixture(troopCount: 3);

        Send(fixture, new[]
        {
            PartyScreenCommits.Transfer(
                PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, Server.GetHandle<CharacterObject>(fixture.TroopId), 3),
        });

        Server.Call(() =>
        {
            var state = Resolve(fixture);
            Assert.NotNull(state.Settlement.Town.GarrisonParty);
            Assert.Equal(3, state.Settlement.Town.GarrisonParty.MemberRoster.GetTroopCount(state.Troop));
            Assert.Equal(0, state.Party.MemberRoster.GetTroopCount(state.Troop));
        });
    }

    // A player party in an owned town with no garrison yet.
    private GarrisonFixture CreateFixture(int troopCount)
    {
        GarrisonFixture fixture = default;
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            var ownerClan = GameObjectCreator.CreateInitializedObject<Clan>();
            var troop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            settlement.Town = town;
            settlement.SetSettlementComponent(town);
            town.OwnerClan = ownerClan;
            town.IsOwnerUnassigned = false;
            party.CurrentSettlement = settlement;
            party.MemberRoster.AddToCounts(troop, troopCount);

            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(settlement, out var settlementId));
            Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("RequestingControllerId", heroId, string.Empty, string.Empty, string.Empty)));
            playerManager.SetPeer("RequestingControllerId", Client.NetPeer);

            fixture = new GarrisonFixture(heroId, settlementId, troopId);
        });
        TestEnvironment.FlushCoalescer();
        return fixture;
    }

    private void Send(GarrisonFixture fixture, PartyScreenCommandData[] commands)
    {
        var message = PartyScreenCommits.Create(
            fixture.MainHeroId,
            Helpers.PartyScreenHelper.PartyScreenMode.TroopsManage,
            PartyScreenDoneKind.DonateGarrison,
            commands,
            isDonating: true,
            doNotApplyGoldTransactions: false);

        Client.Call(() => Client.Resolve<INetwork>().SendAll(message));
        TestEnvironment.FlushCoalescer();
    }

    private (Settlement Settlement, MobileParty Party, CharacterObject Troop) Resolve(GarrisonFixture fixture)
    {
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.MainHeroId, out var hero));
        Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
        Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(fixture.TroopId, out var troop));
        return (settlement, hero.PartyBelongedTo, troop);
    }

    public void Dispose() => TestEnvironment.Dispose();

    private readonly record struct GarrisonFixture(string MainHeroId, string SettlementId, string TroopId);
}
