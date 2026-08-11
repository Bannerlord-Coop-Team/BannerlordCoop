using Coop.Core.Server.Services.MobileParties;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
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

    private static void SetFixtureXp(TroopRoster roster, CharacterObject character, int xp)
    {
        int index = roster.FindIndexOfTroop(character);
        var element = roster.data[index];
        element._xp = xp;
        roster.data[index] = element;
    }
}
