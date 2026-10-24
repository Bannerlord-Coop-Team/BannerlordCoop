using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// Normal party-screen commits that release or take prisoners and recruit the same type in one Done.
/// </summary>
public class PartyDoneLogicPrisonerRecruitTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.Single();

    public PartyDoneLogicPrisonerRecruitTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
    }

    [Fact]
    public void ReleaseAndRecruitOfSameType_AppliesBoth()
    {
        var fixture = CreateFixture(prisonerCount: 3);

        Send(CreateMessage(fixture, released: 1, recruited: 1, prisonerDelta: -2));

        AssertCounts(fixture, prisoners: 1, members: 1);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public void TakeAndRecruitOfSameType_AppliesBoth(int taken, int recruited)
    {
        var fixture = CreateFixture(prisonerCount: 1);

        Send(CreateMessage(fixture, released: 0, recruited: recruited, prisonerDelta: taken - recruited, taken: taken));

        AssertCounts(fixture, prisoners: 1 + taken - recruited, members: recruited);
    }

    [Fact]
    public void ReleaseAndRecruitOfDifferentTypes_AppliesBoth()
    {
        var fixture = CreateFixture(prisonerCount: 2);
        var other = CreateFixture(prisonerCount: 1, fixture.MainHeroId);

        Send(CreateMessage(
            fixture,
            released: 1,
            recruited: 0,
            prisonerDelta: -1,
            recruitedFixture: other));

        AssertCounts(fixture, prisoners: 1, members: 0);
        AssertCounts(other, prisoners: 0, members: 1);
    }

    [Fact]
    public void ReleaseNotMatchingPrisonerDelta_IsRejected()
    {
        var fixture = CreateFixture(prisonerCount: 3);

        // One release and no recruit cannot remove two prisoners.
        Send(CreateMessage(fixture, released: 1, recruited: 0, prisonerDelta: -2));

        AssertCounts(fixture, prisoners: 3, members: 0);
    }

    [Fact]
    public void RecruitCountedAsRelease_IsRejected()
    {
        var fixture = CreateFixture(prisonerCount: 3);

        // Recruits leave the prison roster, so a recruit plus a release of two only covers three.
        Send(CreateMessage(fixture, released: 2, recruited: 1, prisonerDelta: -2));

        AssertCounts(fixture, prisoners: 3, members: 0);
    }

    private PrisonerFixture CreateFixture(int prisonerCount, string mainHeroId = null)
    {
        PrisonerFixture fixture = default;
        Server.Call(() =>
        {
            MobileParty party;
            if (mainHeroId == null)
            {
                party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            }
            else
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(mainHeroId, out var mainHero));
                party = mainHero.PartyBelongedTo;
            }

            var prisoner = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            party.PrisonRoster.AddToCounts(prisoner, prisonerCount);

            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(prisoner, out var prisonerId));
            fixture = new PrisonerFixture(heroId, prisonerId);
        });
        TestEnvironment.FlushCoalescer();
        return fixture;
    }

    private NetworkCompleteDoneLogic CreateMessage(
        PrisonerFixture fixture,
        int released,
        int recruited,
        int prisonerDelta,
        PrisonerFixture? recruitedFixture = null,
        int taken = 0)
    {
        uint prisonerHandle = Server.GetHandle<CharacterObject>(fixture.PrisonerId);
        var prisonerDeltas = new List<TroopRosterElementData>();
        // PackTroopRosterDelta leaves out an unchanged stack.
        if (prisonerDelta != 0)
            prisonerDeltas.Add(new TroopRosterElementData(prisonerHandle, prisonerDelta, 0, 0));
        var memberDeltas = new List<TroopRosterElementData>();
        if (recruited != 0)
            memberDeltas.Add(new TroopRosterElementData(prisonerHandle, recruited, 0, 0));

        var recruitedPrisoners = Troops(fixture.PrisonerId, recruited, firstSeed: 100).ToList();
        if (recruitedFixture.HasValue)
        {
            uint otherHandle = Server.GetHandle<CharacterObject>(recruitedFixture.Value.PrisonerId);
            prisonerDeltas.Add(new TroopRosterElementData(otherHandle, -1, 0, 0));
            memberDeltas.Add(new TroopRosterElementData(otherHandle, 1, 0, 0));
            recruitedPrisoners.AddRange(Troops(recruitedFixture.Value.PrisonerId, 1, firstSeed: 200));
        }

        return new NetworkCompleteDoneLogic(
            fixture.MainHeroId,
            Troops(fixture.PrisonerId, released, firstSeed: 1).ToArray(),
            Troops(fixture.PrisonerId, taken, firstSeed: 300).ToArray(),
            recruitedPrisoners.ToArray(),
            EmptyRosterData(),
            EmptyRosterData(),
            new TroopRosterData(memberDeltas.ToArray()),
            new TroopRosterData(prisonerDeltas.ToArray()),
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
            applyReleasedAndTakenPrisonerActions: true);
    }

    private static IEnumerable<FlattenedTroop> Troops(string characterId, int count, int firstSeed)
        => Enumerable.Range(firstSeed, count).Select(seed => new FlattenedTroop(
            characterId,
            isHero: false,
            uniqueSeed: seed,
            RosterTroopState.Active,
            xp: 0,
            xpGained: 0));

    private void Send(NetworkCompleteDoneLogic message)
    {
        Client.Call(() => Client.Resolve<INetwork>().SendAll(message));
        TestEnvironment.FlushCoalescer();
    }

    private void AssertCounts(PrisonerFixture fixture, int prisoners, int members)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.MainHeroId, out var mainHero));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(fixture.PrisonerId, out var prisoner));
            var party = mainHero.PartyBelongedTo;
            Assert.Equal(prisoners, party.PrisonRoster.GetTroopCount(prisoner));
            Assert.Equal(members, party.MemberRoster.GetTroopCount(prisoner));
        });
    }

    private static TroopRosterData EmptyRosterData() =>
        new(Array.Empty<TroopRosterElementData>());

    public void Dispose() => TestEnvironment.Dispose();

    private readonly record struct PrisonerFixture(string MainHeroId, string PrisonerId);
}
