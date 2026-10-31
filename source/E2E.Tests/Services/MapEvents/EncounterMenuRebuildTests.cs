using Common.Util;
using E2E.Tests.Environment.Instance;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>Covers the client encounter menu rebuild once the server confirms the main party's battle join.</summary>
public class EncounterMenuRebuildTests : MapEventTestBase
{
    private const string AttackOption = "attack";
    private const string LeaveOption = "leave";

    /// <summary>Builds a fresh server and client environment per test.</summary>
    public EncounterMenuRebuildTests(ITestOutputHelper output) : base(output) { }

    /// <summary>Attack is hidden when "Help defenders" opens the menu before the join lands, and must show once the
    /// server's battle party attach arrives, which needs the option list rebuilt, not only its conditions.</summary>
    [Fact]
    public void JoinConfirmedAfterMenuBuilt_RebuildsOptionsAndShowsAttack()
    {
        var (ctx, partyId, client) = SetupJoinerBeforeAttach();
        var handler = new Mock<IMenuContextHandler>();
        string[] shownOnRefresh = Array.Empty<string>();
        handler.Setup(h => h.OnMenuRefresh()).Callback(() => shownOnRefresh = VisibleOptionIds());
        OpenEncounterMenu(client, handler.Object);

        client.Call(() => Assert.Equal(new[] { LeaveOption }, VisibleOptionIds()));

        JoinServerPartyToSide(ctx.MapEventId, partyId, BattleSideEnum.Defender);

        client.Call(() => Assert.NotNull(MobileParty.MainParty.MapEvent));
        handler.Verify(h => h.OnMenuRefresh(), Times.Once);
        Assert.Equal(new[] { AttackOption, LeaveOption }, shownOnRefresh);
    }

    /// <summary>A menu no view has bound yet only re-evaluates its conditions instead of throwing on the null handler.</summary>
    [Fact]
    public void JoinConfirmedWithUnboundMenu_RefreshesConditionsWithoutThrowing()
    {
        var (ctx, partyId, client) = SetupJoinerBeforeAttach();
        OpenEncounterMenu(client, handler: null);

        JoinServerPartyToSide(ctx.MapEventId, partyId, BattleSideEnum.Defender);

        client.Call(() =>
        {
            Assert.NotNull(MobileParty.MainParty.MapEvent);
            Assert.Equal(new[] { AttackOption, LeaveOption }, VisibleOptionIds());
        });
    }

    /// <summary>Seeds a client main party that is not in the battle yet, with an encounter already holding the event.</summary>
    private (MapEventContext ctx, string partyId, EnvironmentInstance client) SetupJoinerBeforeAttach()
    {
        var ctx = CreateServerMapEvent();
        var partyId = CreateServerParty();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
            Assert.Null(MobileParty.MainParty.MapEvent);
        }, MapEventDisabledMethods);
        SetMockPlayerEncounter(client, mapEventId: ctx.MapEventId);

        return (ctx, partyId, client);
    }

    /// <summary>Opens an "encounter" menu whose Attack option, like vanilla's, needs the main party in a map event.</summary>
    private static void OpenEncounterMenu(EnvironmentInstance client, IMenuContextHandler? handler)
    {
        client.Call(() =>
        {
            var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
            starter.AddGameMenu("encounter", "Encounter", _ => { });
            starter.AddGameMenuOption("encounter", AttackOption, "Attack!", _ => MobileParty.MainParty.MapEvent != null, _ => { });
            starter.AddGameMenuOption("encounter", LeaveOption, "Leave...", _ => true, _ => { });

            var state = Game.Current.GameStateManager.CreateState<MapState>();
            state._menuContext = ObjectHelper.SkipConstructor<MenuContext>();
            state._menuContext.GameMenu = Campaign.Current.GameMenuManager.GetGameMenu("encounter");
            state._menuContext.Handler = handler;
            Game.Current.GameStateManager._gameStates.Add(state);
        });
    }

    /// <summary>Lists the options a menu view shows when it rebuilds, the way <c>GameMenuVM.Refresh(true)</c> picks them.</summary>
    private static string[] VisibleOptionIds()
    {
        var menuManager = Campaign.Current.GameMenuManager;
        var menuContext = Campaign.Current.CurrentMenuContext;
        return Enumerable.Range(0, menuManager.GetVirtualMenuOptionAmount(menuContext))
            .Where(i => menuManager.GetVirtualMenuOptionConditionsHold(menuContext, i))
            .Select(i => menuManager.GetVirtualGameMenuOption(menuContext, i).IdString)
            .ToArray();
    }
}
