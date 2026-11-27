using E2E.Tests.Environment;
using HarmonyLib;
using SandBox.View.Menu;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.GameMenus;

/// <summary>
/// A menu whose option condition throws while the map screen builds its menu view (#4866).
/// </summary>
public class MenuViewContextPatchesTests : IDisposable
{
    private const string BrokenMenu = "coop_broken_menu_test";

    private static readonly List<MenuPanel> Panels = new();

    private readonly E2ETestEnvironment environment;
    private readonly Harmony presentationPatch = new($"coop-menu-view-context-{Guid.NewGuid()}");

    public MenuViewContextPatchesTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        Panels.Clear();
    }

    public void Dispose()
    {
        presentationPatch.UnpatchAll(presentationPatch.Id);
        Panels.Clear();
        environment.Dispose();
    }

    // MapScreen.UpdateMenuView builds a new MenuViewContext every frame until one opens.
    [Fact]
    public void MenuThatFailsToOpen_LeavesNoPanelBehindPerRetry()
    {
        // Swap the Gauntlet menu panel for one that refreshes the same GameMenuVM without a render layer.
        presentationPatch.Patch(
            AccessTools.Method(typeof(MenuViewContext), "TaleWorlds.CampaignSystem.GameState.IMenuContextHandler.OnMenuCreate"),
            prefix: new HarmonyMethod(typeof(MenuViewContextPatchesTests), nameof(AddMenuPanel)));

        var client = environment.Clients.First();
        client.Call(() =>
        {
            var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
            starter.AddGameMenu(BrokenMenu, "", _ => { });
            // The #2342 throw: a hideout option read the encounter battle after the encounter closed.
            starter.AddGameMenuOption(BrokenMenu, "join", "", _ => PlayerEncounter.EncounteredBattle != null, _ => { });
            var context = Game.Current.ObjectManager.CreateObject<MenuContext>();
            context.SwitchToMenu(BrokenMenu);
            Assert.Null(PlayerEncounter.Current);

            for (var frame = 0; frame < 3; frame++)
            {
                Assert.Throws<NullReferenceException>(() => new MenuViewContext(null, context));
            }

            Assert.Equal(3, Panels.Count);
            Assert.All(Panels, panel => Assert.True(panel.Released));
            Assert.Null(context.Handler);
        });
    }

    public static bool AddMenuPanel(MenuViewContext __instance)
    {
        Panels.Add((MenuPanel)__instance.AddMenuView<MenuPanel>());
        return false;
    }

    /// <summary>
    /// Stands in for <c>GauntletMenuBaseView</c>, whose open movie is one menu panel on screen.
    /// </summary>
    public class MenuPanel : MenuView
    {
        private GameMenuVM dataSource;

        public bool Released { get; private set; }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            dataSource = new GameMenuVM(MenuContext);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            dataSource.Refresh(forceUpdateItems: true);
        }

        protected override void OnFinalize()
        {
            dataSource.OnFinalize();
            Released = true;
            base.OnFinalize();
        }
    }
}
