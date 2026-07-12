using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters;

/// <summary>
/// Behavioural contract for the party-screen "discard"/transfer flow: when the authoritative server
/// changes the troop counts of a party's member roster, every client must converge to the same counts,
/// and a hero element must be left untouched by a regular-troop change.
///
/// This intentionally drives the underlying roster-mutation sync (server AddToCounts -> TroopRosterPatches
/// -> broadcast -> client apply) rather than the PartyScreenLogic/PartyDoneLogic plumbing, so the test is
/// independent of how the done-logic assembles its payload. It must therefore pass identically whether the
/// server rebuilds rosters from absolute data or applies a client-computed delta.
/// </summary>
public class TroopRosterDiscardSyncTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public TroopRosterDiscardSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void ServerDiscardsRegularTroops_SyncsToAllClients_LeavesHeroUntouched()
    {
        string partyId = null;
        string heroCharacterId = null;
        string regularCharacterId = null;

        // Arrange: a party whose member roster holds a hero (count 1) and a stack of regulars (count 5).
        // These AddToCounts run on the server outside an AllowedThread, so they broadcast to the clients.
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            var regular = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
            party.MemberRoster.AddToCounts(regular, 5);

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out heroCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(regular, out regularCharacterId));
        });
        TestEnvironment.FlushCoalescer();

        // Sanity: every client mirrors the initial roster before the discard.
        AssertCountsOnClients(partyId, heroCharacterId, expectedHero: 1, regularCharacterId, expectedRegular: 5);

        // Act: discard 2 regulars on the server (the net effect of a party-screen discard).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(regularCharacterId, out var regular));

            party.MemberRoster.AddToCounts(regular, -2);
        });
        TestEnvironment.FlushCoalescer();

        // Assert: the regulars dropped to 3 on every client and the hero is still present at 1.
        AssertCountsOnClients(partyId, heroCharacterId, expectedHero: 1, regularCharacterId, expectedRegular: 3);
    }

    private void AssertCountsOnClients(string partyId, string heroCharacterId, int expectedHero, string regularCharacterId, int expectedRegular)
    {
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(heroCharacterId, out var heroCharacter));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(regularCharacterId, out var regularCharacter));

            var roster = party.MemberRoster;
            Assert.Equal(expectedHero, roster.GetTroopCount(heroCharacter));
            Assert.Equal(expectedRegular, roster.GetTroopCount(regularCharacter));
        }
    }

    public void Dispose() => TestEnvironment.Dispose();
}
