using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using Common.Util;
using GameInterface.Services.TroopRosters.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters;

/// <summary>
/// Behavioural contract for the party-screen transfer flow: when the authoritative server moves troops out
/// of one party's member roster and into another's (the "left" and "right" parties of the party screen),
/// every client must converge to the same counts on BOTH rosters, and each party's hero must be left
/// untouched by the transfer of regular troops.
///
/// Like <see cref="TroopRosterDiscardSyncTests"/> this drives the underlying roster-mutation sync (server
/// AddToCounts -> TroopRosterPatches -> broadcast -> client apply) rather than the PartyDoneLogic plumbing,
/// so it passes identically whether the server rebuilds rosters from absolute data or applies a
/// client-computed delta.
/// </summary>
public class TroopRosterTransferSyncTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public TroopRosterTransferSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void ServerTransfersRegulars_BetweenParties_SyncsBothRostersToAllClients_LeavesHeroesUntouched()
    {
        string rightPartyId = null;
        string leftPartyId = null;
        string rightHeroCharacterId = null;
        string leftHeroCharacterId = null;
        string regularCharacterId = null;

        // Arrange: a "right" party holding a hero (count 1) and a stack of regulars (count 5), and a "left"
        // party holding only its own hero (count 1). These AddToCounts run on the server outside an
        // AllowedThread, so they broadcast to the clients.
        Server.Call(() =>
        {
            var rightParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var leftParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var rightHero = GameObjectCreator.CreateInitializedObject<Hero>();
            var leftHero = GameObjectCreator.CreateInitializedObject<Hero>();
            var regular = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            rightParty.MemberRoster.AddToCounts(rightHero.CharacterObject, 1);
            rightParty.MemberRoster.AddToCounts(regular, 5);
            leftParty.MemberRoster.AddToCounts(leftHero.CharacterObject, 1);

            Assert.True(Server.ObjectManager.TryGetId(rightParty, out rightPartyId));
            Assert.True(Server.ObjectManager.TryGetId(leftParty, out leftPartyId));
            Assert.True(Server.ObjectManager.TryGetId(rightHero.CharacterObject, out rightHeroCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(leftHero.CharacterObject, out leftHeroCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(regular, out regularCharacterId));
        });
        TestEnvironment.FlushCoalescer();

        // Sanity: clients mirror the initial split (regulars only on the right party).
        AssertTroopCountOnClients(rightPartyId, regularCharacterId, expectedCount: 5);
        AssertTroopCountOnClients(leftPartyId, regularCharacterId, expectedCount: 0);

        // Act: transfer 3 regulars from the right party to the left party.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(rightPartyId, out var rightParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leftPartyId, out var leftParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(regularCharacterId, out var regular));

            rightParty.MemberRoster.AddToCounts(regular, -3);
            leftParty.MemberRoster.AddToCounts(regular, 3);
        });
        TestEnvironment.FlushCoalescer();

        // Assert: the regulars moved on every client, and each party's hero is still present at 1.
        AssertTroopCountOnClients(rightPartyId, regularCharacterId, expectedCount: 2);
        AssertTroopCountOnClients(leftPartyId, regularCharacterId, expectedCount: 3);
        AssertTroopCountOnClients(rightPartyId, rightHeroCharacterId, expectedCount: 1);
        AssertTroopCountOnClients(leftPartyId, leftHeroCharacterId, expectedCount: 1);
    }

    [Fact]
    public void ServerAddsAbsentHeroTwice_LeavesRosterAndNetworkUnchanged()
    {
        string partyId = null;
        string heroCharacterId = null;

        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out heroCharacterId));

            party.MemberRoster.AddToCounts(hero.CharacterObject, 2);

            Assert.Equal(0, party.MemberRoster.GetTroopCount(hero.CharacterObject));
            Assert.DoesNotContain(Server.InternalMessages.OfType<CountsAtIndexAdded>(),
                message => message.TroopRoster == party.MemberRoster && message.Character == hero.CharacterObject);
        });
        TestEnvironment.FlushCoalescer();

        AssertTroopCountOnClients(partyId, heroCharacterId, expectedCount: 0);
    }

    [Fact]
    public void ServerRemovesHeroFromMalformedCountThree_SyncsRemainingTwo()
    {
        string partyId = null;
        string heroCharacterId = null;

        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out heroCharacterId));
        });
        TestEnvironment.FlushCoalescer();

        SetHeroCountLocally(Server, partyId, heroCharacterId, 3);
        foreach (var client in Clients)
        {
            SetHeroCountLocally(client, partyId, heroCharacterId, 3);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(heroCharacterId, out var heroCharacter));
            party.MemberRoster.RemoveTroop(heroCharacter);
        });
        TestEnvironment.FlushCoalescer();

        Assert.Equal(2, GetTroopCount(Server, partyId, heroCharacterId));
        AssertTroopCountOnClients(partyId, heroCharacterId, expectedCount: 2);
    }

    private void AssertTroopCountOnClients(string partyId, string characterId, int expectedCount)
    {
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            Assert.Equal(expectedCount, party.MemberRoster.GetTroopCount(character));
        }
    }

    private static void SetHeroCountLocally(
        EnvironmentInstance instance,
        string partyId,
        string characterId,
        int count)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            int index = party.MemberRoster.FindIndexOfTroop(character);
            Assert.True(index >= 0);
            using (new AllowedThread())
            {
                party.MemberRoster.data[index]._number = count;
                party.MemberRoster._totalHeroes = count;
            }
        });
    }

    private static int GetTroopCount(
        EnvironmentInstance instance,
        string partyId,
        string characterId)
    {
        int count = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            count = party.MemberRoster.GetTroopCount(character);
        });
        return count;
    }

    public void Dispose() => TestEnvironment.Dispose();
}
