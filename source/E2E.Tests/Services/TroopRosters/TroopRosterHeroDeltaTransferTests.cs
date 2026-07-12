using Common.Network.Coalescing;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace E2E.Tests.Services.TroopRosters;

/// <summary>
/// Exercises the delta apply path (<see cref="ITroopRosterInterface.ApplyTroopRosterDeltas"/>) for the two
/// cases where a roster element is a <see cref="Hero"/> and therefore carries party linkage that AddToCounts
/// mutates as a side effect: a companion (member roster) and a prisoner (prison roster).
///
/// The transferred hero starts in the right party and moves to the left party, with the destination (+1)
/// listed before the source (-1) - the order that would null the hero's party linkage if removals and
/// additions were not split into separate passes. This guards the remove-before-add behaviour.
/// </summary>
public class TroopRosterHeroDeltaTransferTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public TroopRosterHeroDeltaTransferTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    private static TroopRosterData Delta(string characterId, int number)
        => new TroopRosterData(new[] { new TroopRosterElementData(characterId, number, 0, 0) });

    [Fact]
    public void CompanionTransfer_MainToOtherParty_ViaDelta_SyncsRostersAndPartyBelongedTo()
    {
        string rightPartyId = null;
        string leftPartyId = null;
        string companionId = null;
        string companionCharacterId = null;

        // Arrange: a companion (a count-1 hero) sitting in the right/main party's member roster.
        Server.Call(() =>
        {
            var rightParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var leftParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var companion = GameObjectCreator.CreateInitializedObject<Hero>();

            rightParty.MemberRoster.AddToCounts(companion.CharacterObject, 1);

            Assert.True(Server.ObjectManager.TryGetId(rightParty, out rightPartyId));
            Assert.True(Server.ObjectManager.TryGetId(leftParty, out leftPartyId));
            Assert.True(Server.ObjectManager.TryGetId(companion, out companionId));
            Assert.True(Server.ObjectManager.TryGetId(companion.CharacterObject, out companionCharacterId));
        });
        TestEnvironment.FlushCoalescer();
        CreateFreedCoalescerSlots();

        // Act: transfer the companion right -> left via the batched delta apply. The destination (+1) is
        // listed before the source (-1) to prove the two-pass apply is order-independent.
        Server.Call(() =>
        {
            var troopRosterInterface = Server.Resolve<ITroopRosterInterface>();
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(rightPartyId, out var rightParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leftPartyId, out var leftParty));

            troopRosterInterface.ApplyTroopRosterDeltas(new[]
            {
                (leftParty.MemberRoster, Delta(companionCharacterId, 1)),
                (rightParty.MemberRoster, Delta(companionCharacterId, -1)),
            });
        });
        TestEnvironment.FlushCoalescer();

        // Assert: the companion moved on every client and its PartyBelongedTo points at the left party.
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(rightPartyId, out var rightParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leftPartyId, out var leftParty));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));

            Assert.Equal(0, rightParty.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(1, leftParty.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Same(leftParty, companion.PartyBelongedTo);
        }
    }

    [Fact]
    public void PrisonerTransfer_MainToOtherParty_ViaDelta_SyncsRostersAndPartyBelongedToAsPrisoner()
    {
        string rightPartyId = null;
        string leftPartyId = null;
        string prisonerId = null;
        string prisonerCharacterId = null;

        // Arrange: a prisoner (a hero) held in the right/main party's prison roster.
        Server.Call(() =>
        {
            var rightParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var leftParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var prisoner = GameObjectCreator.CreateInitializedObject<Hero>();

            rightParty.Party.PrisonRoster.AddToCounts(prisoner.CharacterObject, 1);

            Assert.True(Server.ObjectManager.TryGetId(rightParty, out rightPartyId));
            Assert.True(Server.ObjectManager.TryGetId(leftParty, out leftPartyId));
            Assert.True(Server.ObjectManager.TryGetId(prisoner, out prisonerId));
            Assert.True(Server.ObjectManager.TryGetId(prisoner.CharacterObject, out prisonerCharacterId));
        });
        TestEnvironment.FlushCoalescer();
        CreateFreedCoalescerSlots();

        // Act: transfer the prisoner right -> left via the batched delta apply (destination listed first).
        Server.Call(() =>
        {
            var troopRosterInterface = Server.Resolve<ITroopRosterInterface>();
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(rightPartyId, out var rightParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leftPartyId, out var leftParty));

            troopRosterInterface.ApplyTroopRosterDeltas(new[]
            {
                (leftParty.Party.PrisonRoster, Delta(prisonerCharacterId, 1)),
                (rightParty.Party.PrisonRoster, Delta(prisonerCharacterId, -1)),
            });
        });
        TestEnvironment.FlushCoalescer();

        // Assert: the prisoner moved on every client and its PartyBelongedToAsPrisoner points at the left party.
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(rightPartyId, out var rightParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leftPartyId, out var leftParty));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(prisonerId, out var prisoner));

            Assert.Equal(0, rightParty.Party.PrisonRoster.GetTroopCount(prisoner.CharacterObject));
            Assert.Equal(1, leftParty.Party.PrisonRoster.GetTroopCount(prisoner.CharacterObject));
            Assert.Same(leftParty.Party, prisoner.PartyBelongedToAsPrisoner);
        }
    }

    private void CreateFreedCoalescerSlots()
    {
        Server.Call(() =>
        {
            var fillerRoster = GameObjectCreator.CreateInitializedObject<TroopRoster>();
            var firstFiller = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var secondFiller = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            fillerRoster.AddToCounts(firstFiller, 1);
            fillerRoster.AddToCounts(secondFiller, 1);

            Assert.True(Server.ObjectManager.TryGetId(fillerRoster, out var fillerRosterId));
            fillerRosterId = Compact(fillerRosterId, typeof(TroopRoster));

            var coalescer = Server.Resolve<ISendCoalescer>();
            Assert.True(coalescer.HasPending);

            // Removing both keys leaves reusable Dictionary entry slots. Before hero AddCounts became
            // immediate, the next source/destination transfer reused those slots in reverse enumeration
            // order and replayed the destination add before the source removal.
            coalescer.DropInstance(fillerRosterId);
            Assert.False(coalescer.HasPending);
        });
    }

    public void Dispose() => TestEnvironment.Dispose();
}
