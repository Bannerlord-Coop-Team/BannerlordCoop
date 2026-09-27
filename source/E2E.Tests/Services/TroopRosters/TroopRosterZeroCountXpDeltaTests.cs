using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters;

/// <summary>
/// Party-screen deltas that empty a stack whose server xp sits above the vanilla cap. Vanilla clamps xp only in
/// SetElementXp, so an xp-free count drop leaves the server above the cap while the client roster clamps.
/// </summary>
public class TroopRosterZeroCountXpDeltaTests : IDisposable
{
    // (Level + 6)^2 - 10 conformity per prisoner.
    private const int SkirmisherLevel = 21;
    private const int SkirmisherCap = 719;
    private const int VeteranLevel = 26;
    private const int VeteranCap = 1014;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public TroopRosterZeroCountXpDeltaTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        InstallXpCapModels();
    }

    [Theory]
    // Logged #3274 state: the client removes the capped xp of (1,0,1438).
    [InlineData(1, 2 * SkirmisherCap, -1, -SkirmisherCap, true)]
    [InlineData(1, 2 * SkirmisherCap, -1, 0, true)]
    [InlineData(2, 2 * SkirmisherCap, -1, -SkirmisherCap, true)]
    // A server xp gain within the cap is still a concurrent change.
    [InlineData(1, 700, -1, -650, false)]
    [InlineData(1, 2 * SkirmisherCap, -1, -700, false)]
    [InlineData(1, SkirmisherCap, -1, -2 * SkirmisherCap, false)]
    [InlineData(1, 2 * SkirmisherCap, -2, -2 * SkirmisherCap, false)]
    public void PrisonerStackDelta_DropsOnlyXpAboveVanillaCap(
        int serverNumber,
        int serverXp,
        int numberDelta,
        int xpDelta,
        bool expectedApplied)
    {
        var fixture = CreateStack(prisoner: true, SkirmisherLevel, serverNumber, 0, serverXp);

        Server.Call(() =>
        {
            var (roster, character) = Resolve(fixture);
            var applied = Server.Resolve<ITroopRosterInterface>().TryApplyTroopRosterDeltas(new[]
            {
                (roster, Delta(Handle(fixture), numberDelta, 0, xpDelta)),
            });

            Assert.Equal(expectedApplied, applied);
            if (!applied)
            {
                AssertElement(roster, character, serverNumber, 0, serverXp);
            }
            else if (serverNumber + numberDelta == 0)
            {
                Assert.Equal(-1, roster.FindIndexOfTroop(character));
            }
            else
            {
                AssertElement(roster, character, serverNumber + numberDelta, 0, serverXp + xpDelta);
            }
        });
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            var (roster, character) = Resolve(client, fixture);
            int expectedNumber = expectedApplied ? serverNumber + numberDelta : serverNumber;
            Assert.Equal(expectedNumber, roster.GetTroopCount(character));
        }
    }

    [Fact]
    public void EmptyingStackAboveVanillaCap_AppliesRemovalWithoutXp()
    {
        var fixture = CreateStack(prisoner: true, SkirmisherLevel, 1, 0, 2 * SkirmisherCap);
        Server.InternalMessages.Clear();
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            var (roster, _) = Resolve(fixture);
            Assert.True(Server.Resolve<ITroopRosterInterface>().TryApplyTroopRosterDeltas(new[]
            {
                (roster, Delta(Handle(fixture), -1, 0, -SkirmisherCap)),
            }));
        });
        TestEnvironment.FlushCoalescer();

        var added = Assert.Single(Server.InternalMessages.OfType<CountsAtIndexAdded>());
        Assert.Equal(-1, added.CountChange);
        Assert.Equal(0, added.XpChange);
        Assert.True(added.RemoveDepleted);
        var operation = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTroopRosterElementBatch>()
            .SelectMany(batch => batch.Operations));
        Assert.Equal(TroopRosterElementOperationKind.AddCounts, operation.Kind);
        Assert.Equal(-1, operation.Count);
        Assert.Equal(0, operation.Xp);
    }

    [Fact]
    public void RecruitAll_AppliesPrisonAndMemberTogether()
    {
        // Logged #3161 state (1,0,4056), recruited in the same commit as a stack at the cap.
        var aboveCap = CreateStack(prisoner: true, VeteranLevel, 1, 0, 4 * VeteranCap);
        var atCap = CreateStack(prisoner: true, SkirmisherLevel, 1, 0, SkirmisherCap, aboveCap.PartyId);

        Server.Call(() =>
        {
            var (prisonRoster, aboveCapCharacter) = Resolve(aboveCap);
            var (_, atCapCharacter) = Resolve(atCap);
            var memberRoster = prisonRoster.OwnerParty.MemberRoster;

            Assert.True(Server.Resolve<ITroopRosterInterface>().TryApplyTroopRosterDeltas(new[]
            {
                (prisonRoster, new TroopRosterData(new[]
                {
                    new TroopRosterElementData(Handle(aboveCap), -1, 0, -VeteranCap),
                    new TroopRosterElementData(Handle(atCap), -1, 0, -SkirmisherCap),
                })),
                (memberRoster, new TroopRosterData(new[]
                {
                    new TroopRosterElementData(Handle(aboveCap), 1, 0, 0),
                    new TroopRosterElementData(Handle(atCap), 1, 0, 0),
                })),
            }));

            Assert.Equal(0, prisonRoster.GetTroopCount(aboveCapCharacter));
            Assert.Equal(0, prisonRoster.GetTroopCount(atCapCharacter));
            Assert.Equal(1, memberRoster.GetTroopCount(aboveCapCharacter));
            Assert.Equal(1, memberRoster.GetTroopCount(atCapCharacter));
        });
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            var (prisonRoster, character) = Resolve(client, aboveCap);
            Assert.Equal(0, prisonRoster.GetTroopCount(character));
            Assert.Equal(1, prisonRoster.OwnerParty.MemberRoster.GetTroopCount(character));
        }
    }

    [Fact]
    public void DonateAll_TwoRosterDelta_IsAccepted()
    {
        // Logged #3274 state (2,0,3042) moved whole into another prison roster.
        var fixture = CreateStack(prisoner: true, VeteranLevel, 2, 0, 3 * VeteranCap);
        string destinationPartyId = null;
        Server.Call(() =>
        {
            var destination = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(destination, out destinationPartyId));
        });
        TestEnvironment.FlushCoalescer();

        Server.Call(() =>
        {
            var (source, character) = Resolve(fixture);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(destinationPartyId, out var destination));

            Assert.True(Server.Resolve<ITroopRosterInterface>().TryApplyTroopRosterDeltas(new[]
            {
                (destination.PrisonRoster, Delta(Handle(fixture), 2, 0, 2 * VeteranCap)),
                (source, Delta(Handle(fixture), -2, 0, -2 * VeteranCap)),
            }));

            Assert.Equal(-1, source.FindIndexOfTroop(character));
            AssertElement(destination.PrisonRoster, character, 2, 0, 2 * VeteranCap);
        });
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            var (source, character) = Resolve(client, fixture);
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(destinationPartyId, out var destination));
            Assert.Equal(0, source.GetTroopCount(character));
            Assert.Equal(2, destination.PrisonRoster.GetTroopCount(character));
        }
    }

    [Fact]
    public void UpgradeAll_WoundedMemberStackAboveVanillaCap_IsAccepted()
    {
        // Logged #3274 state (2,1,3900): 1300 xp per troop from tier 4 to tier 5.
        string targetId = null;
        Server.Call(() =>
        {
            var target = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            target.Level = VeteranLevel;
            Assert.True(Server.ObjectManager.TryGetId(target, out targetId));
        });
        var fixture = CreateStack(prisoner: false, SkirmisherLevel, 2, 1, 3900, upgradeTargetId: targetId);

        Server.Call(() =>
        {
            var (roster, character) = Resolve(fixture);
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(targetId, out var target));

            Assert.True(Server.Resolve<ITroopRosterInterface>().TryApplyTroopRosterDeltas(new[]
            {
                (roster, new TroopRosterData(new[]
                {
                    new TroopRosterElementData(Handle(fixture), -2, -1, -2600),
                    new TroopRosterElementData(Server.GetHandle<CharacterObject>(targetId), 2, 1, 0),
                })),
            }));

            Assert.Equal(-1, roster.FindIndexOfTroop(character));
            AssertElement(roster, target, 2, 1, 0);
        });
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            var (roster, character) = Resolve(client, fixture);
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(targetId, out var target));
            Assert.Equal(0, roster.GetTroopCount(character));
            Assert.Equal(2, roster.GetTroopCount(target));
        }
    }

    [Fact]
    public void XpOnlyDeltaOnZeroCountElement_IsRejected()
    {
        var fixture = CreateStack(prisoner: true, SkirmisherLevel, 1, 0, SkirmisherCap);

        Server.Call(() =>
        {
            var (roster, character) = Resolve(fixture);
            roster.AddToCounts(character, -1, removeDepleted: false);
            AssertElement(roster, character, 0, 0, SkirmisherCap);

            Assert.False(Server.Resolve<ITroopRosterInterface>().TryApplyTroopRosterDeltas(new[]
            {
                (roster, Delta(Handle(fixture), 0, 0, -100)),
            }));

            AssertElement(roster, character, 0, 0, SkirmisherCap);
        });
    }

    [Fact]
    public void UnownedRoster_EmptyingStackWithSurplusXp_IsRejected()
    {
        Server.Call(() =>
        {
            var roster = GameObjectCreator.CreateInitializedObject<TroopRoster>();
            var character = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            character.Level = SkirmisherLevel;
            int index = roster.AddToCounts(character, 1);
            roster.SetElementXp(index, 2 * SkirmisherCap);
            Assert.True(Server.ObjectManager.TryGetHandle(character, out var characterId));

            // No owner party means no vanilla cap, so the surplus is real xp.
            Assert.False(Server.Resolve<ITroopRosterInterface>().TryApplyTroopRosterDeltas(new[]
            {
                (roster, Delta(characterId, -1, 0, -SkirmisherCap)),
            }));

            AssertElement(roster, character, 1, 0, 2 * SkirmisherCap);
        });
    }

    private StackFixture CreateStack(
        bool prisoner,
        int level,
        int number,
        int wounded,
        int xp,
        string partyId = null,
        string upgradeTargetId = null)
    {
        StackFixture fixture = default;
        Server.Call(() =>
        {
            MobileParty party;
            if (partyId == null)
                party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            else
                Assert.True(Server.ObjectManager.TryGetObject(partyId, out party));

            var character = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            character.Level = level;
            if (upgradeTargetId != null)
            {
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(upgradeTargetId, out var target));
                character.UpgradeTargets = new[] { target };
            }

            // Fill a larger stack to the cap, then drop the extra troops without xp like a death or transfer.
            var roster = prisoner ? party.PrisonRoster : party.MemberRoster;
            int perTroopCap = prisoner
                ? character.ConformityNeededToRecruitPrisoner
                : character.GetUpgradeXpCost(party.Party, 0);
            int extra = Math.Max(0, ((xp + perTroopCap - 1) / perTroopCap) - number);
            int index = roster.AddToCounts(character, number + extra, false, wounded);
            roster.SetElementXp(index, xp);
            if (extra != 0) roster.AddToCounts(character, -extra);
            AssertElement(roster, character, number, wounded, xp);

            Assert.True(Server.ObjectManager.TryGetId(party, out var id));
            Assert.True(Server.ObjectManager.TryGetId(character, out var characterId));
            fixture = new StackFixture(id, characterId, prisoner);
        });
        TestEnvironment.FlushCoalescer();
        return fixture;
    }

    private (TroopRoster roster, CharacterObject character) Resolve(StackFixture fixture)
        => Resolve(Server, fixture);

    private static (TroopRoster roster, CharacterObject character) Resolve(
        EnvironmentInstance instance,
        StackFixture fixture)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(fixture.PartyId, out var party));
        Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(fixture.CharacterId, out var character));
        return (fixture.IsPrisoner ? party.PrisonRoster : party.MemberRoster, character);
    }

    private uint Handle(StackFixture fixture) => Server.GetHandle<CharacterObject>(fixture.CharacterId);

    private static TroopRosterData Delta(uint characterId, int number, int wounded, int xp) =>
        new(new[] { new TroopRosterElementData(characterId, number, wounded, xp) });

    private static void AssertElement(TroopRoster roster, CharacterObject character, int number, int wounded, int xp)
    {
        int index = roster.FindIndexOfTroop(character);
        Assert.True(index >= 0);
        var element = roster.GetElementCopyAtIndex(index);
        Assert.Equal((number, wounded, xp), (element.Number, element.WoundedNumber, element.Xp));
    }

    // The harness Campaign boots without models; the vanilla xp cap reads these three.
    private void InstallXpCapModels()
    {
        Server.Call(() =>
        {
            if (Campaign.Current.Models != null) return;

            var models = new List<GameModel>
            {
                new DefaultCharacterStatsModel(),
                new DefaultPartyTroopUpgradeModel(),
                new DefaultPrisonerRecruitmentCalculationModel(),
            };
            var gameModels = Server.GameInstance.Game.AddGameModelsManager<GameModels>(models);
            AccessTools.Field(typeof(Campaign), "_gameModels").SetValue(Campaign.Current, gameModels);
        });
    }

    public void Dispose() => TestEnvironment.Dispose();

    private readonly record struct StackFixture(string PartyId, string CharacterId, bool IsPrisoner);
}
