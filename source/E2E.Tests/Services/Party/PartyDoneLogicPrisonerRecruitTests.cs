using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// Normal party screen commits that release or take prisoners and recruit in one Done.
/// </summary>
public class PartyDoneLogicPrisonerRecruitTests : IDisposable
{
    // Enough conformity for every prisoner, the roster caps it at what the stack can use.
    private const int FullConformity = 1_000_000;

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

        Send(fixture, seed: default, Release(fixture, 1), Recruit(fixture, 1));

        AssertCounts(fixture, prisoners: 1, members: 1);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public void TakeAndRecruitOfSameType_AppliesBoth(int taken, int recruited)
    {
        var fixture = CreateFixture(prisonerCount: 1);
        var seed = new TroopRosterData(new[]
        {
            new TroopRosterElementData(Handle(fixture), taken, 0, FullConformity),
        });

        Send(fixture, seed, Take(fixture, taken), Recruit(fixture, recruited));

        AssertCounts(fixture, prisoners: 1 + taken - recruited, members: recruited);
    }

    [Fact]
    public void ReleaseAndRecruitOfDifferentTypes_AppliesBoth()
    {
        var fixture = CreateFixture(prisonerCount: 2);
        var other = CreateFixture(prisonerCount: 1, fixture.MainHeroId);

        Send(fixture, seed: default, Release(fixture, 1), Recruit(other, 1));

        AssertCounts(fixture, prisoners: 1, members: 0);
        AssertCounts(other, prisoners: 0, members: 1);
    }

    [Fact]
    public void StaleRecruit_IsRejectedAlone_AndTheReleaseStillApplies()
    {
        var fixture = CreateFixture(prisonerCount: 3, conformity: 0);

        Send(fixture, seed: default, Recruit(fixture, 1), Release(fixture, 1));

        AssertCounts(fixture, prisoners: 2, members: 0);
    }

    private PrisonerFixture CreateFixture(int prisonerCount, string mainHeroId = null, int conformity = FullConformity)
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
            if (conformity != 0) party.PrisonRoster.AddXpToTroop(prisoner, conformity);

            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(prisoner, out var prisonerId));

            if (mainHeroId == null)
            {
                var playerManager = Server.Resolve<IPlayerManager>();
                Assert.True(playerManager.AddPlayer(new Player("RequestingControllerId", heroId, string.Empty, string.Empty, string.Empty)));
                playerManager.SetPeer("RequestingControllerId", Client.NetPeer);
            }
            fixture = new PrisonerFixture(heroId, prisonerId);
        });
        TestEnvironment.FlushCoalescer();
        return fixture;
    }

    private uint Handle(PrisonerFixture fixture) => Server.GetHandle<CharacterObject>(fixture.PrisonerId);

    private PartyScreenCommandData Release(PrisonerFixture fixture, int number)
        => Command(PartyScreenLogic.PartyCommandCode.TransferTroop, PartyScreenLogic.PartyRosterSide.Right, fixture, number);

    private PartyScreenCommandData Take(PrisonerFixture fixture, int number)
        => Command(PartyScreenLogic.PartyCommandCode.TransferTroop, PartyScreenLogic.PartyRosterSide.Left, fixture, number);

    private PartyScreenCommandData Recruit(PrisonerFixture fixture, int number)
        => Command(PartyScreenLogic.PartyCommandCode.RecruitTroop, PartyScreenLogic.PartyRosterSide.Right, fixture, number);

    private PartyScreenCommandData Command(
        PartyScreenLogic.PartyCommandCode code,
        PartyScreenLogic.PartyRosterSide side,
        PrisonerFixture fixture,
        int number)
        => new(code, side, PartyScreenLogic.TroopType.Prisoner, Handle(fixture), number, 0, 0);

    private void Send(PrisonerFixture fixture, TroopRosterData seed, params PartyScreenCommandData[] commands)
    {
        var message = new NetworkCommitPartyScreen(
            fixture.MainHeroId,
            Helpers.PartyScreenHelper.PartyScreenMode.Normal,
            PartyScreenDoneKind.Default,
            commands,
            new TroopRosterOrderData(new()),
            leftPartyId: null,
            leftMemberSeed: default,
            leftPrisonerSeed: seed,
            isDonating: false,
            doNotApplyGoldTransactions: true,
            forceTransferId: null,
            issueOwnerId: null,
            issueId: null);

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

    public void Dispose() => TestEnvironment.Dispose();

    private readonly record struct PrisonerFixture(string MainHeroId, string PrisonerId);
}
