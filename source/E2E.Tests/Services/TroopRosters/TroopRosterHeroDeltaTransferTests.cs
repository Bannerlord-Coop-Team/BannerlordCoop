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
/// Exercises the delta apply path (<see cref="ITroopRosterInterface.TryApplyTroopRosterDeltas"/>) for the two
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

    private static TroopRosterData Delta(string characterId, int number, int xp = 0)
        => new TroopRosterData(new[] { new TroopRosterElementData(characterId, number, 0, xp) });

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

            Assert.True(troopRosterInterface.TryApplyTroopRosterDeltas(new[]
            {
                (leftParty.MemberRoster, Delta(companionCharacterId, 1)),
                (rightParty.MemberRoster, Delta(companionCharacterId, -1)),
            }, out var rejectionReason), rejectionReason);
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

            Assert.True(troopRosterInterface.TryApplyTroopRosterDeltas(new[]
            {
                (leftParty.Party.PrisonRoster, Delta(prisonerCharacterId, 1)),
                (rightParty.Party.PrisonRoster, Delta(prisonerCharacterId, -1)),
            }, out var rejectionReason), rejectionReason);
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

    [Fact]
    public void FullStackTransferAfterConcurrentSourceXpChange_IsRejectedAtomically()
    {
        string rightPartyId = null;
        string leftPartyId = null;
        string memberCharacterId = null;
        string prisonerCharacterId = null;

        Server.Call(() =>
        {
            var rightParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var leftParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var memberCharacter = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var prisonerCharacter = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            int memberIndex = rightParty.MemberRoster.AddToCounts(memberCharacter, 3);
            rightParty.MemberRoster.data[memberIndex].Xp = 65;
            int prisonerIndex = rightParty.PrisonRoster.AddToCounts(prisonerCharacter, 2);
            rightParty.PrisonRoster.data[prisonerIndex].Xp = 47;

            Assert.True(Server.ObjectManager.TryGetId(rightParty, out rightPartyId));
            Assert.True(Server.ObjectManager.TryGetId(leftParty, out leftPartyId));
            Assert.True(Server.ObjectManager.TryGetId(memberCharacter, out memberCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(prisonerCharacter, out prisonerCharacterId));
        });
        TestEnvironment.FlushCoalescer();

        Server.Call(() =>
        {
            var troopRosterInterface = Server.Resolve<ITroopRosterInterface>();
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(rightPartyId, out var rightParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leftPartyId, out var leftParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(memberCharacterId, out var memberCharacter));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(prisonerCharacterId, out var prisonerCharacter));
            Assert.Equal(65, rightParty.MemberRoster.GetElementXp(
                rightParty.MemberRoster.FindIndexOfTroop(memberCharacter)));
            Assert.Equal(47, rightParty.PrisonRoster.GetElementXp(
                rightParty.PrisonRoster.FindIndexOfTroop(prisonerCharacter)));

            var applied = troopRosterInterface.TryApplyTroopRosterDeltas(new[]
            {
                (leftParty.MemberRoster, Delta(memberCharacterId, 3, 60)),
                (rightParty.MemberRoster, Delta(memberCharacterId, -3, -60)),
                (leftParty.PrisonRoster, Delta(prisonerCharacterId, 2, 40)),
                (rightParty.PrisonRoster, Delta(prisonerCharacterId, -2, -40)),
            }, out var rejectionReason);

            Assert.False(applied);
            Assert.NotEmpty(rejectionReason);
            Assert.Equal(3, rightParty.MemberRoster.GetTroopCount(memberCharacter));
            Assert.Equal(65, rightParty.MemberRoster.GetElementXp(
                rightParty.MemberRoster.FindIndexOfTroop(memberCharacter)));
            Assert.Equal(2, rightParty.PrisonRoster.GetTroopCount(prisonerCharacter));
            Assert.Equal(47, rightParty.PrisonRoster.GetElementXp(
                rightParty.PrisonRoster.FindIndexOfTroop(prisonerCharacter)));
            Assert.Equal(0, leftParty.MemberRoster.GetTroopCount(memberCharacter));
            Assert.Equal(0, leftParty.PrisonRoster.GetTroopCount(prisonerCharacter));
        });
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(rightPartyId, out var rightParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leftPartyId, out var leftParty));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(memberCharacterId, out var memberCharacter));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(prisonerCharacterId, out var prisonerCharacter));

            Assert.Equal(3, rightParty.MemberRoster.GetTroopCount(memberCharacter));
            Assert.Equal(2, rightParty.PrisonRoster.GetTroopCount(prisonerCharacter));
            Assert.Equal(0, leftParty.MemberRoster.GetTroopCount(memberCharacter));
            Assert.Equal(0, leftParty.PrisonRoster.GetTroopCount(prisonerCharacter));
        }
    }

    [Fact]
    public void InvalidDeltaBatch_DoesNotApplyAnyRosterChanges()
    {
        string firstRosterId = null;
        string secondRosterId = null;
        string firstCharacterId = null;
        string secondCharacterId = null;

        Server.Call(() =>
        {
            var firstRoster = GameObjectCreator.CreateInitializedObject<TroopRoster>();
            var secondRoster = GameObjectCreator.CreateInitializedObject<TroopRoster>();
            var firstCharacter = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var secondCharacter = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            firstRoster.AddToCounts(firstCharacter, 2);
            secondRoster.AddToCounts(secondCharacter, 1);

            Assert.True(Server.ObjectManager.TryGetId(firstRoster, out firstRosterId));
            Assert.True(Server.ObjectManager.TryGetId(secondRoster, out secondRosterId));
            Assert.True(Server.ObjectManager.TryGetId(firstCharacter, out firstCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(secondCharacter, out secondCharacterId));
        });
        TestEnvironment.FlushCoalescer();

        Server.Call(() =>
        {
            var troopRosterInterface = Server.Resolve<ITroopRosterInterface>();
            Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(firstRosterId, out var firstRoster));
            Assert.True(Server.ObjectManager.TryGetObject<TroopRoster>(secondRosterId, out var secondRoster));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(firstCharacterId, out var firstCharacter));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(secondCharacterId, out var secondCharacter));

            var applied = troopRosterInterface.TryApplyTroopRosterDeltas(new[]
            {
                (firstRoster, Delta(firstCharacterId, -1)),
                (secondRoster, new TroopRosterData(new[]
                {
                    new TroopRosterElementData(secondCharacterId, 0, -1, 0),
                })),
            }, out var rejectionReason);

            Assert.False(applied);
            Assert.NotEmpty(rejectionReason);
            Assert.Equal(2, firstRoster.GetTroopCount(firstCharacter));
            Assert.Equal(1, secondRoster.GetTroopCount(secondCharacter));
        });
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(firstRosterId, out var firstRoster));
            Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(secondRosterId, out var secondRoster));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(firstCharacterId, out var firstCharacter));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(secondCharacterId, out var secondCharacter));

            Assert.Equal(2, firstRoster.GetTroopCount(firstCharacter));
            Assert.Equal(1, secondRoster.GetTroopCount(secondCharacter));
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
