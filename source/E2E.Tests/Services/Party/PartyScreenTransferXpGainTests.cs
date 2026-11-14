using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party;
using GameInterface.Services.Party.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// A server xp gain on a troop the player moved out of an open party screen, through the client refresher's replay,
/// the Done commit and the server replay, plus server changes that shrink or remove a moved stack and the troop
/// manager popup's save and restore around a replay. The screen binds the real party rosters like vanilla manage troops.
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
    public void TransferThenServerXpGain_KeepsTheTransferWithServerXp()
    {
        var fixture = CreateFixture();
        var logic = OpenPartyScreen(fixture);

        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);
        Assert.Equal(new RosterState((-1, -1), (1, TroopXp)), ReadClient(fixture));

        SetServerXp(fixture, ServerXp);
        AssertScreenReplayed(fixture, logic, new RosterState((-1, -1), (1, ServerXp)));

        Done(fixture, logic);
        AssertServerKeepsTotalXp(fixture, new RosterState((-1, -1), (1, ServerXp)));
    }

    [Fact]
    public void TransferBackAfterServerXpGain_KeepsServerXp()
    {
        var fixture = CreateFixture();
        var logic = OpenPartyScreen(fixture);

        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);
        SetServerXp(fixture, ServerXp);
        AssertScreenReplayed(fixture, logic, new RosterState((-1, -1), (1, ServerXp)));

        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Left);
        Assert.Equal(new RosterState((1, ServerXp), (-1, -1)), ReadClient(fixture));

        Done(fixture, logic);
        AssertServerKeepsTotalXp(fixture, new RosterState((1, ServerXp), (-1, -1)));
    }

    [Fact]
    public void ServerRemovesPartOfAMovedStack_KeepsWhatIsLeftAndSaysSo()
    {
        var fixture = CreateFixture(troopCount: 3);
        var logic = OpenPartyScreen(fixture);
        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right, count: 3);

        var shown = CaptureShownMessages(() => SetServerCount(fixture, 1));

        Assert.Equal((-1, 1), ReadClientNumbers(fixture, fixture.TroopId));
        Assert.EndsWith(" x3: only 1 still possible", Assert.Single(shown));
    }

    [Fact]
    public void ServerRemovesAWholeMovedStack_DropsThatMoveAndReplaysTheRest()
    {
        var fixture = CreateFixture(troopCount: 3);
        var logic = OpenPartyScreen(fixture);
        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right, count: 3);
        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right, otherTroop: true);

        var shown = CaptureShownMessages(() => SetServerCount(fixture, 0));

        Assert.Equal((-1, -1), ReadClientNumbers(fixture, fixture.TroopId));
        Assert.Equal((-1, 1), ReadClientNumbers(fixture, fixture.OtherTroopId));
        Assert.EndsWith(" x3: no longer possible", Assert.Single(shown));
    }

    // The troop manager popup saves the screen on open and restores it on cancel. A server update while it is open
    // replays the moves on both sides of the save, and the cancel still keeps only the move from before it.
    [Fact]
    public void PopupCancelAfterServerUpdate_KeepsOnlyTheMoveBeforeThePopup()
    {
        var fixture = CreateFixture(troopCount: 3);
        var logic = OpenPartyScreen(fixture);
        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);
        Client.Call(() => logic.SavePartyScreenData());
        Transfer(fixture, logic, PartyScreenLogic.PartyRosterSide.Right);

        SetServerXp(fixture, ServerXp);
        Assert.Equal((1, 2), ReadClientNumbers(fixture, fixture.TroopId));

        Client.Call(() =>
        {
            using (new AllowedThread()) logic.ResetToLastSavedPartyScreenData(true);
            Assert.Single(Client.Resolve<IPartyScreenCommandTracker>().GetCommands(logic));
        });
        Assert.Equal((2, 1), ReadClientNumbers(fixture, fixture.TroopId));
    }

    // The gain lands in the baseline and the pending transfer is replayed on top of it, so the screen
    // keeps the player's move and the moved troop carries the server's xp.
    private void AssertScreenReplayed(TransferFixture fixture, PartyScreenLogic logic, RosterState expected)
    {
        Assert.Equal(expected, ReadClient(fixture));

        RosterState baseline = default;
        Client.Call(() =>
        {
            var (_, _, troop) = Resolve(Client, fixture);
            baseline = new RosterState(
                Read(logic._initialData.RightMemberRoster, troop),
                Read(logic._initialData.LeftMemberRoster, troop));
        });
        Assert.Equal(new RosterState((1, ServerXp), (-1, -1)), baseline);
    }

    private void AssertServerKeepsTotalXp(TransferFixture fixture, RosterState expected)
    {
        var committed = ReadServer(fixture);
        Assert.Equal(expected, committed);
        Assert.Equal(ServerXp, Math.Max(committed.Right.Xp, 0) + Math.Max(committed.Left.Xp, 0));
    }

    private TransferFixture CreateFixture(int troopCount = 1)
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
            var otherTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            otherTroop.UpgradeTargets = Array.Empty<CharacterObject>();

            Assert.True(Server.ObjectManager.TryGetId(right, out var rightId));
            Assert.True(Server.ObjectManager.TryGetId(left, out var leftId));
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));
            Assert.True(Server.ObjectManager.TryGetId(target, out var targetId));
            Assert.True(Server.ObjectManager.TryGetId(otherTroop, out var otherTroopId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(ControllerId, heroId, rightId, null, null)));
            fixture = new TransferFixture(rightId, leftId, heroId, troopId, targetId, otherTroopId);
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        TestEnvironment.FlushCoalescer();

        // Upgrade targets are not synced, and the client roster clamps xp with them.
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(fixture.TroopId, out var troop));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(fixture.TargetId, out var target));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(fixture.OtherTroopId, out var otherTroop));
            using (new AllowedThread())
            {
                troop.Level = TroopLevel;
                target.Level = UpgradeTargetLevel;
                troop.UpgradeTargets = new[] { target };
                otherTroop.UpgradeTargets = Array.Empty<CharacterObject>();
            }
        });

        Server.Call(() =>
        {
            var (right, _, troop) = Resolve(Server, fixture);
            right.MemberRoster.AddToCounts(troop, troopCount, false, 0, TroopXp);
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(fixture.OtherTroopId, out var otherTroop));
            right.MemberRoster.AddToCounts(otherTroop, 1);
        });
        TestEnvironment.FlushCoalescer();

        Assert.Equal(new RosterState((troopCount, TroopXp), (-1, -1)), ReadClient(fixture));
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

    private void Transfer(
        TransferFixture fixture,
        PartyScreenLogic logic,
        PartyScreenLogic.PartyRosterSide fromSide,
        int count = 1,
        bool otherTroop = false)
    {
        Client.Call(() =>
        {
            var (_, _, troop) = Resolve(Client, fixture);
            if (otherTroop) Assert.True(Client.ObjectManager.TryGetObject(fixture.OtherTroopId, out troop));
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(fromSide, PartyScreenLogic.TroopType.Member, troop, count, 0, -1);
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
            Client.SimulateMessage(this, PartyScreenLogicPatches.CreateCommitAttempted(
                logic, hero, Helpers.PartyScreenHelper.PartyScreenMode.Normal, isDonating: false, forceTransferId: null));
        });
        TestEnvironment.FlushCoalescer();
    }

    private void SetServerCount(TransferFixture fixture, int count)
    {
        Server.Call(() =>
        {
            var (right, _, troop) = Resolve(Server, fixture);
            right.MemberRoster.AddToCounts(troop, count - right.MemberRoster.GetTroopRoster().First(element => element.Character == troop).Number);
        });
        TestEnvironment.FlushCoalescer();
    }

    private static readonly List<string> shownMessages = new();

    private static List<string> CaptureShownMessages(Action act)
    {
        var harmony = new Harmony($"e2e.party-screen-replay.{Guid.NewGuid():N}");
        shownMessages.Clear();
        harmony.Patch(
            AccessTools.Method(typeof(InformationManager), nameof(InformationManager.DisplayMessage)),
            prefix: new HarmonyMethod(typeof(PartyScreenTransferXpGainTests), nameof(CaptureShownMessage)));
        try
        {
            act();
            return shownMessages.ToList();
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            shownMessages.Clear();
        }
    }

    private static bool CaptureShownMessage(InformationMessage message)
    {
        shownMessages.Add(message.Information);
        return false;
    }

    // The troop's number on the right and the left of the client's screen, -1 without a row.
    private (int Right, int Left) ReadClientNumbers(TransferFixture fixture, string troopId)
    {
        (int, int) numbers = default;
        Client.Call(() =>
        {
            var (right, left, _) = Resolve(Client, fixture);
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            numbers = (Read(right.MemberRoster, troop).Number, Read(left.MemberRoster, troop).Number);
        });
        return numbers;
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
        string TargetId,
        string OtherTroopId);

    private readonly record struct RosterState((int Number, int Xp) Right, (int Number, int Xp) Left);
}
