using Coop.Core.Server.Services.MobileParties;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.MobileParties.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace E2E.Tests.Services.MobileParties;

public class PlayerPartyTroopXpBaselineProviderTests : SyncTestBase
{
    public PlayerPartyTroopXpBaselineProviderTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Capture_IncludesOnlyTheJoiningPlayersMemberAndPrisonerXp()
    {
        EnvironmentInstance joiningClient = Clients.First();
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string memberId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string prisonerId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(memberId, out var member));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(prisonerId, out var prisoner));
            party.MemberRoster.AddToCounts(member, 3);
            party.PrisonRoster.AddToCounts(prisoner, 2);
            party.MemberRoster.GetTroopRoster();
            party.PrisonRoster.GetTroopRoster();
            SetFixtureXp(party.MemberRoster, member, 123);
            SetFixtureXp(party.PrisonRoster, prisoner, 456);
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player("joining", null, partyId, null, null)));
        });
        TestEnvironment.ConnectRegisteredPlayer(joiningClient, "joining");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetId(party.MemberRoster, out var memberRosterId));
            Assert.True(Server.ObjectManager.TryGetId(party.PrisonRoster, out var prisonRosterId));
            var provider = Server.Resolve<IPlayerPartyTroopXpBaselineProvider>();
            Assert.True(provider.TryCapture(joiningClient.NetPeer, out var baselines));
            Assert.Collection(baselines,
                members =>
                {
                    Assert.Equal(Compact(memberRosterId, typeof(TroopRoster)), members.RosterId);
                    var entry = Assert.Single(members.Entries,
                        candidate => candidate.CharacterId == Compact(memberId, typeof(CharacterObject)));
                    Assert.Equal(123, entry.Xp);
                },
                prisoners =>
                {
                    Assert.Equal(Compact(prisonRosterId, typeof(TroopRoster)), prisoners.RosterId);
                    var entry = Assert.Single(prisoners.Entries,
                        candidate => candidate.CharacterId == Compact(prisonerId, typeof(CharacterObject)));
                    Assert.Equal(456, entry.Xp);
                });
        });
    }

    [Fact]
    public void Capture_IncludesSameClanCompanionPartyButExcludesWorldAiAndOtherPlayerParties()
    {
        EnvironmentInstance joiningClient = Clients.First();
        string playerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string companionPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string worldAiPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string otherPlayerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        string worldClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        string memberId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string prisonerId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string companionMemberId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string companionPrisonerId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string excludedMemberId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(companionPartyId, out var companionParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(worldAiPartyId, out var worldAiParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherPlayerPartyId, out var otherPlayerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(worldClanId, out var worldClan));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(memberId, out var member));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(prisonerId, out var prisoner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(companionMemberId, out var companionMember));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(companionPrisonerId, out var companionPrisoner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(excludedMemberId, out var excludedMember));

            playerParty.ActualClan = playerClan;
            companionParty.ActualClan = playerClan;
            otherPlayerParty.ActualClan = playerClan;
            worldAiParty.ActualClan = worldClan;

            playerParty.MemberRoster.AddToCounts(member, 3);
            playerParty.PrisonRoster.AddToCounts(prisoner, 2);
            companionParty.MemberRoster.AddToCounts(companionMember, 4);
            companionParty.PrisonRoster.AddToCounts(companionPrisoner, 1);
            worldAiParty.MemberRoster.AddToCounts(excludedMember, 5);
            otherPlayerParty.MemberRoster.AddToCounts(excludedMember, 6);
            SetFixtureXp(playerParty.MemberRoster, member, 123);
            SetFixtureXp(playerParty.PrisonRoster, prisoner, 456);
            SetFixtureXp(companionParty.MemberRoster, companionMember, 789);
            SetFixtureXp(companionParty.PrisonRoster, companionPrisoner, 987);
            SetFixtureXp(worldAiParty.MemberRoster, excludedMember, 111);
            SetFixtureXp(otherPlayerParty.MemberRoster, excludedMember, 222);

            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player("joining-clan-player", null, playerPartyId, playerClanId, null)));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player("other-clan-player", null, otherPlayerPartyId, playerClanId, null)));
            Assert.False(companionParty.IsPlayerParty());
        });
        TestEnvironment.ConnectRegisteredPlayer(joiningClient, "joining-clan-player");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(companionPartyId, out var companionParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(worldAiPartyId, out var worldAiParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherPlayerPartyId, out var otherPlayerParty));
            Assert.True(Server.ObjectManager.TryGetId(playerParty.MemberRoster, out var playerMemberRosterId));
            Assert.True(Server.ObjectManager.TryGetId(playerParty.PrisonRoster, out var playerPrisonRosterId));
            Assert.True(Server.ObjectManager.TryGetId(companionParty.MemberRoster, out var companionMemberRosterId));
            Assert.True(Server.ObjectManager.TryGetId(companionParty.PrisonRoster, out var companionPrisonRosterId));
            Assert.True(Server.ObjectManager.TryGetId(worldAiParty.MemberRoster, out var worldAiMemberRosterId));
            Assert.True(Server.ObjectManager.TryGetId(otherPlayerParty.MemberRoster, out var otherPlayerMemberRosterId));

            var provider = Server.Resolve<IPlayerPartyTroopXpBaselineProvider>();
            Assert.True(provider.TryCapture(joiningClient.NetPeer, out var baselines));
            Assert.Equal(4, baselines.Length);
            Assert.Equal(Compact(playerMemberRosterId, typeof(TroopRoster)), baselines[0].RosterId);
            Assert.Equal(Compact(playerPrisonRosterId, typeof(TroopRoster)), baselines[1].RosterId);

            var companionMembers = Assert.Single(baselines,
                baseline => baseline.RosterId == Compact(companionMemberRosterId, typeof(TroopRoster)));
            Assert.Equal(789, Assert.Single(companionMembers.Entries,
                entry => entry.CharacterId == Compact(companionMemberId, typeof(CharacterObject))).Xp);
            var companionPrisoners = Assert.Single(baselines,
                baseline => baseline.RosterId == Compact(companionPrisonRosterId, typeof(TroopRoster)));
            Assert.Equal(987, Assert.Single(companionPrisoners.Entries,
                entry => entry.CharacterId == Compact(companionPrisonerId, typeof(CharacterObject))).Xp);
            Assert.DoesNotContain(baselines,
                baseline => baseline.RosterId == Compact(worldAiMemberRosterId, typeof(TroopRoster)));
            Assert.DoesNotContain(baselines,
                baseline => baseline.RosterId == Compact(otherPlayerMemberRosterId, typeof(TroopRoster)));
        });
    }

    [Fact]
    public void Capture_IncludesPlayerOwnedGarrisonMemberAndPrisonerXp()
    {
        EnvironmentInstance joiningClient = Clients.First();
        string playerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        string memberId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string prisonerId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string garrisonPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(memberId, out var member));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(prisonerId, out var prisoner));
            playerParty.ActualClan = playerClan;

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            playerClan.InitMembers();
            settlement.SetSettlementComponent(town);
            town.OwnerClan = playerClan;
            var garrisonParty = GarrisonPartyComponent.CreateGarrisonParty("Issue3039BaselineGarrison", settlement);

            Assert.True(Server.ObjectManager.TryGetId(garrisonParty, out garrisonPartyId));
            Assert.Null(garrisonParty.ActualClan);
            garrisonParty.MemberRoster.AddToCounts(member, 3);
            garrisonParty.PrisonRoster.AddToCounts(prisoner, 2);
            SetFixtureXp(garrisonParty.MemberRoster, member, 654);
            SetFixtureXp(garrisonParty.PrisonRoster, prisoner, 321);
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player("joining-garrison-player", null, playerPartyId, playerClanId, null)));
        });
        TestEnvironment.ConnectRegisteredPlayer(joiningClient, "joining-garrison-player");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(garrisonPartyId, out var garrisonParty));
            Assert.True(Server.ObjectManager.TryGetId(garrisonParty.MemberRoster, out var memberRosterId));
            Assert.True(Server.ObjectManager.TryGetId(garrisonParty.PrisonRoster, out var prisonRosterId));

            var provider = Server.Resolve<IPlayerPartyTroopXpBaselineProvider>();
            Assert.True(provider.TryCapture(joiningClient.NetPeer, out var baselines));

            var members = Assert.Single(baselines,
                baseline => baseline.RosterId == Compact(memberRosterId, typeof(TroopRoster)));
            Assert.Equal(654, Assert.Single(members.Entries,
                entry => entry.CharacterId == Compact(memberId, typeof(CharacterObject))).Xp);
            var prisoners = Assert.Single(baselines,
                baseline => baseline.RosterId == Compact(prisonRosterId, typeof(TroopRoster)));
            Assert.Equal(321, Assert.Single(prisoners.Entries,
                entry => entry.CharacterId == Compact(prisonerId, typeof(CharacterObject))).Xp);
        });
    }

    private static void SetFixtureXp(TroopRoster roster, CharacterObject character, int xp)
    {
        int index = roster.FindIndexOfTroop(character);
        var element = roster.data[index];
        element._xp = xp;
        roster.data[index] = element;
    }
}
