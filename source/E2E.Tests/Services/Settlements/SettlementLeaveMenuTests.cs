using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using GameInterface.Services.Settlements.Patches;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

public class SettlementLeaveMenuTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public SettlementLeaveMenuTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public static IEnumerable<object[]> MenuStates()
    {
        foreach (string menu in new[] { "town_outside", "castle_outside", "siege_attacker_left", "siege_attacker_defeated" })
        foreach (string state in new[] { "orphaned", "other-party", "other-menu", "no-menu", "active-encounter", "battle-side", "siege-camp", "mission-state" })
            yield return new object[] { menu, state, state == "orphaned" };
    }

    [Theory]
    [MemberData(nameof(MenuStates))]
    public void ServerDrivenLeave_ClosesOnlyOrphanedSettlementMenu(string settlementMenu, string state, bool closesMenu)
    {
        var client = environment.Clients.First();
        var leavingId = environment.CreateRegisteredObject<MobileParty>();
        var otherId = environment.CreateRegisteredObject<MobileParty>();
        PlayerEncounter originalEncounter = null;
        uint leavingHandle = 0;
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leavingId, out var leaving));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(otherId, out var other));
            Assert.True(client.ObjectManager.TryGetHandle(leaving, out leavingHandle));
            Campaign.Current.MainParty = state == "other-party" ? other : leaving;
            Campaign.Current.PlayerEncounter = null;
            leaving._currentSettlement = null;
            leaving.Party._mapEventSide = null;
            leaving._besiegerCamp = null;

            var states = Game.Current.GameStateManager;
            var map = states.CreateState<MapState>();
            states._gameStates.Add(map);
            if (state != "no-menu")
            {
                string menuId = state == "other-menu" ? "encounter" : settlementMenu;
                // Seed the orphaned menu without running settlement-dependent vanilla initialization.
                map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
                map._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
                map._menuContext.GameMenu.StringId = menuId;
                Assert.Equal(menuId, Campaign.Current.CurrentMenuContext.GameMenu.StringId);
            }

            if (state == "active-encounter")
            {
                originalEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
                Campaign.Current.PlayerEncounter = originalEncounter;
            }
            if (state == "battle-side")
                leaving.Party._mapEventSide = ObjectHelper.SkipConstructor<MapEventSide>();
            if (state == "siege-camp")
                leaving._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
            if (state == "mission-state")
            {
                states._gameStates.Add(ObjectHelper.SkipConstructor<MissionState>());
                Assert.IsType<MissionState>(states.ActiveState);
            }
            else
            {
                Assert.Same(map, states.ActiveState);
            }
        });

        using var menuExit = new MethodCallRecorder(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)));
        using var encounterFinish = new MethodCallRecorder(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)));
        client.SimulateMessage(environment.Server.NetPeer, new NetworkPartyLeaveSettlement(leavingHandle));

        Assert.Equal(closesMenu ? 1 : 0, menuExit.CountFor(client));
        Assert.Equal(0, menuExit.CountFor(environment.Clients.Last()));
        Assert.Equal(0, encounterFinish.Count);
        client.Call(() => Assert.Same(originalEncounter, PlayerEncounter.Current));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CastleInitialization_RequiresEncounterSettlement(bool hasSettlement)
    {
        environment.Clients.First().Call(() =>
        {
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterSettlementAux = hasSettlement ? ObjectHelper.SkipConstructor<Settlement>() : null;
            Campaign.Current.PlayerEncounter = encounter;
            var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_castle_outside_on_init");
            Assert.Contains(Harmony.GetPatchInfo(method).Prefixes,
                patch => patch.PatchMethod == AccessTools.Method(typeof(EncounterGameMenuBehaviorPatch), nameof(EncounterGameMenuBehaviorPatch.CastleOutsidePrefix)));
            Assert.Equal(hasSettlement, EncounterGameMenuBehaviorPatch.CastleOutsidePrefix(null));
            if (!hasSettlement)
                ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>().game_menu_castle_outside_on_init(null);
        });
    }

    public void Dispose() => environment.Dispose();
}
