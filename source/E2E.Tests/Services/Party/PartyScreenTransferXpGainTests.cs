using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// A server xp gain on a troop the player moved out of an open party screen, through the client refresher,
/// the Done delta and the server apply. The screen binds the real party rosters like vanilla manage troops.
/// </summary>
public class PartyScreenTransferXpGainTests : IDisposable
{
    private const string ControllerId = "transfer-xp-player";
    // Tier 1 to tier 2 costs 300 xp, so the troop holds up to 300 xp.
    private const int TroopLevel = 6;
    private const int UpgradeTargetLevel = 11;
    private const int TroopXp = 100;
    private const int ServerXp = 150;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.Single();

    public PartyScreenTransferXpGainTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
        XpCapModels.Install(Server);
        XpCapModels.Install(Client);
    }

    [Fact]
    public void TransferThenServerXpGain_ResetsScreenAndKeepsServerXp()
    {
        var fixture = CreateFixture();
        var logic = OpenPartyScreen(fixture);

        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);
        Assert.Equal(new RosterState((-1, -1), (1, TroopXp)), ReadClient(fixture));

        SetServerXp(fixture, ServerXp);
        AssertScreenReset(fixture, logic);

        Done(fixture, logic);
        AssertServerKeepsTotalXp(fixture, new RosterState((1, ServerXp), (-1, -1)));
    }

    [Fact]
    public void TransferAgainAfterReset_CommitsServerXp()
    {
        var fixture = CreateFixture();
        var logic = OpenPartyScreen(fixture);

        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);
        SetServerXp(fixture, ServerXp);
        AssertScreenReset(fixture, logic);

        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);
        Assert.Equal(new RosterState((-1, -1), (1, ServerXp)), ReadClient(fixture));

        Done(fixture, logic);
        AssertServerKeepsTotalXp(fixture, new RosterState((-1, -1), (1, ServerXp)));
    }

    [Fact]
    public void TransferBackBeforeDone_KeepsServerXp()
    {
        var fixture = CreateFixture();
        var logic = OpenPartyScreen(fixture);

        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);
        SetServerXp(fixture, ServerXp);
        AssertScreenReset(fixture, logic);

        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);
        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Left);
        Assert.Equal(new RosterState((1, ServerXp), (-1, -1)), ReadClient(fixture));

        Done(fixture, logic);
        AssertServerKeepsTotalXp(fixture, new RosterState((1, ServerXp), (-1, -1)));
    }

    // The refresher cannot rebase the moved troop onto the gain, so it drops the pending transfer and the
    // screen shows the server stack again, with the new xp as the baseline for the next Done.
    private void AssertScreenReset(TransferFixture fixture, PartyScreenLogic logic)
    {
        var expected = new RosterState((1, ServerXp), (-1, -1));
        Assert.Equal(expected, ReadClient(fixture));

        RosterState baseline = default;
        Client.Call(() =>
        {
            var (_, _, troop) = Resolve(Client, fixture);
            baseline = new RosterState(
                Read(logic._initialData.RightMemberRoster, troop),
                Read(logic._initialData.LeftMemberRoster, troop));
        });
        Assert.Equal(expected, baseline);
    }

    private void AssertServerKeepsTotalXp(TransferFixture fixture, RosterState expected)
    {
        var committed = ReadServer(fixture);
        Assert.Equal(expected, committed);
        Assert.Equal(ServerXp, Math.Max(committed.Right.Xp, 0) + Math.Max(committed.Left.Xp, 0));
    }

    private TransferFixture CreateFixture()
    {
        TransferFixture fixture = default;
        Server.Call(() =>
        {
            var right = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var left = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var hero = right.LeaderHero;
            // The server apply reads mainHero.PartyBelongedTo.
            if (hero.PartyBelongedTo == null) hero.PartyBelongedTo = right;
            // One clan, so the server sends the left party's rosters to this player like a clan garrison.
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            right._actualClan = clan;
            left._actualClan = clan;

            var target = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            target.Level = UpgradeTargetLevel;
            var troop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            troop.Level = TroopLevel;
            troop.UpgradeTargets = new[] { target };

            Assert.True(Server.ObjectManager.TryGetId(right, out var rightId));
            Assert.True(Server.ObjectManager.TryGetId(left, out var leftId));
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));
            Assert.True(Server.ObjectManager.TryGetId(target, out var targetId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(ControllerId, null, rightId, null, null)));
            fixture = new TransferFixture(rightId, leftId, heroId, troopId, targetId);
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        TestEnvironment.FlushCoalescer();

        // Upgrade targets are not synced, and the client roster clamps xp with them.
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(fixture.TroopId, out var troop));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(fixture.TargetId, out var target));
            using (new AllowedThread())
            {
                troop.Level = TroopLevel;
                target.Level = UpgradeTargetLevel;
                troop.UpgradeTargets = new[] { target };
            }
        });

        Server.Call(() =>
        {
            var (right, _, troop) = Resolve(Server, fixture);
            right.MemberRoster.AddToCounts(troop, 1, false, 0, TroopXp);
        });
        TestEnvironment.FlushCoalescer();

        Assert.Equal(new RosterState((1, TroopXp), (-1, -1)), ReadClient(fixture));
        return fixture;
    }

    private PartyScreenLogic OpenPartyScreen(TransferFixture fixture)
    {
        PartyScreenLogic logic = null!;
        Client.Call(() =>
        {
            var (right, left, _) = Resolve(Client, fixture);
            logic = new PartyScreenLogic();
            logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = right.MemberRoster;
            logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = right.PrisonRoster;
            logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = left.MemberRoster;
            logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = left.PrisonRoster;
            logic.RightOwnerParty = right.Party;
            logic.LeftOwnerParty = left.Party;
            logic.CurrentData.BindRostersFrom(
                right.MemberRoster,
                right.PrisonRoster,
                left.MemberRoster,
                left.PrisonRoster,
                right.Party,
                left.Party);
            logic._initialData.InitializeCopyFrom(right.Party, left.Party);
            logic._initialData.CopyFromPartyAndRoster(
                right.MemberRoster,
                right.PrisonRoster,
                left.MemberRoster,
                left.PrisonRoster,
                right.Party);

            var states = Game.Current.GameStateManager;
            var partyState = states.CreateState<PartyState>();
            partyState.PartyScreenLogic = logic;
            states._gameStates.Add(partyState);
        });
        return logic;
    }

    private void Transfer(TransferFixture fixture, PartyScreenLogic logic, PartyScreenLogic.PartyRosterSide fromSide)
    {
        Client.Call(() =>
        {
            var (_, _, troop) = Resolve(Client, fixture);
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(fromSide, PartyScreenLogic.TroopType.Member, troop, 1, 0, -1);
            // Like PartyCharacterVM.ApplyTransfer, which runs on an allowed thread and then drops emptied rows.
            using (new AllowedThread())
            {
                logic.TransferTroop(command, false);
                logic.RemoveZeroCounts();
            }
        });
    }

    private void SetServerXp(TransferFixture fixture, int xp)
    {
        Server.Call(() =>
        {
            var (right, _, troop) = Resolve(Server, fixture);
            right.MemberRoster.SetElementXp(right.MemberRoster.FindIndexOfTroop(troop), xp);
        });
        TestEnvironment.FlushCoalescer();
    }

    private void Done(TransferFixture fixture, PartyScreenLogic logic)
    {
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var hero));
            var message = new PartyDoneLogicAttempted(
                hero,
                new FlattenedTroopRoster(4),
                new FlattenedTroopRoster(4),
                new FlattenedTroopRoster(4),
                logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left],
                logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left],
                logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right],
                logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right],
                logic._initialData.LeftMemberRoster,
                logic._initialData.LeftPrisonerRoster,
                logic._initialData.RightMemberRoster,
                logic._initialData.RightPrisonerRoster,
                logic.RightOwnerParty.ItemRoster,
                logic.CurrentData.UpgradedTroopsHistory,
                logic.CurrentData.LeftParty,
                partyGoldChangeAmount: 0,
                partyInfluenceChangeAmount: 0,
                partyMoraleChangeAmount: 0,
                doNotApplyGoldTransactions: true,
                Helpers.PartyScreenHelper.PartyScreenMode.Normal);

            Client.SimulateMessage(this, message);
        });
        TestEnvironment.FlushCoalescer();
    }

    private RosterState ReadClient(TransferFixture fixture) => Read(Client, fixture);

    private RosterState ReadServer(TransferFixture fixture) => Read(Server, fixture);

    private static RosterState Read(EnvironmentInstance instance, TransferFixture fixture)
    {
        RosterState state = default;
        instance.Call(() =>
        {
            var (right, left, troop) = Resolve(instance, fixture);
            state = new RosterState(Read(right.MemberRoster, troop), Read(left.MemberRoster, troop));
        });
        return state;
    }

    // (-1, -1) when the roster has no row for the troop.
    private static (int Number, int Xp) Read(TroopRoster roster, CharacterObject troop)
    {
        int index = roster.FindIndexOfTroop(troop);
        if (index < 0) return (-1, -1);

        var element = roster.GetElementCopyAtIndex(index);
        return (element.Number, element.Xp);
    }

    private static (MobileParty right, MobileParty left, CharacterObject troop) Resolve(
        EnvironmentInstance instance,
        TransferFixture fixture)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(fixture.RightPartyId, out var right));
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(fixture.LeftPartyId, out var left));
        Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(fixture.TroopId, out var troop));
        return (right, left, troop);
    }

    public void Dispose() => TestEnvironment.Dispose();

    private readonly record struct TransferFixture(
        string RightPartyId,
        string LeftPartyId,
        string HeroId,
        string TroopId,
        string TargetId);

    private readonly record struct RosterState((int Number, int Xp) Right, (int Number, int Xp) Left);
}
