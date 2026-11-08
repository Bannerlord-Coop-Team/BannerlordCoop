using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

/// <summary>
/// Loads a save whose stored menu is a settlement menu and checks that MapState.OnLoadingFinished
/// no longer runs that menu's init when there is no settlement to show it for, and that a captive
/// server main hero doesn't open the captivity menu after the load.
/// </summary>
public class SavedMenuLoadTests : IDisposable
{
    private const string OtherMenu = "coop_saved_menu_test";

    private readonly E2ETestEnvironment environment;

    public SavedMenuLoadTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    // The reported crash: encounter_menu_ui_castle_on_init reads a null Settlement.CurrentSettlement.
    [Theory]
    [InlineData("town_outside")]
    [InlineData("castle_outside")]
    public void ClientLoad_OutsideMenuWithoutSettlement_OpensOnMap(string menuId)
    {
        var client = environment.Clients.First();
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        client.Call(() =>
        {
            var mapState = PrepareLoad(client, menuId, partyId);
            Assert.Null(Settlement.CurrentSettlement);

            mapState.OnLoadingFinished();

            AssertOnMap(mapState);
        });
    }

    [Fact]
    public void ClientLoad_SettlementOverlayMenuWithoutSettlement_OpensOnMap()
    {
        var client = environment.Clients.First();
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        client.Call(() =>
        {
            var mapState = PrepareLoad(client, "town_keep_test", partyId);
            new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager)
                .AddGameMenu("town_keep_test", "", _ => Assert.Fail("settlement menu init ran"),
                    GameMenu.MenuOverlayType.SettlementWithCharacters);

            mapState.OnLoadingFinished();

            AssertOnMap(mapState);
        });
    }

    [Fact]
    public void ClientLoad_OutsideMenuInsideSettlement_KeepsMenu()
    {
        var client = environment.Clients.First();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();

        client.Call(() =>
        {
            var mapState = PrepareLoad(client, "town_outside", partyId);
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            var town = ObjectHelper.SkipConstructor<Town>();
            town.WaitMeshName = "town_wait_test";
            settlement.SettlementComponent = town;
            MobileParty.MainParty._currentSettlement = settlement;

            mapState.OnLoadingFinished();

            Assert.True(mapState.AtMenu);
            Assert.Equal("town_outside", mapState.MenuContext.GameMenu.StringId);
            Assert.Equal("town_outside", mapState.GameMenuId);
            Assert.Equal("town_wait_test", mapState.MenuContext.CurrentBackgroundMeshName);
        });
    }

    [Fact]
    public void ClientLoad_OtherMenuWithoutSettlement_KeepsMenu()
    {
        var client = environment.Clients.First();
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        client.Call(() =>
        {
            var mapState = PrepareLoad(client, OtherMenu, partyId);
            new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager)
                .AddGameMenu(OtherMenu, "", _ => { });

            mapState.OnLoadingFinished();

            Assert.True(mapState.AtMenu);
            Assert.Equal(OtherMenu, mapState.MenuContext.GameMenu.StringId);
            Assert.Equal(OtherMenu, mapState.GameMenuId);
        });
    }

    [Theory]
    [InlineData("town_outside")]
    [InlineData(OtherMenu)]
    public void ServerLoad_AnyStoredMenu_OpensOnMapWithoutEncounter(string menuId)
    {
        var server = environment.Server;
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        server.Call(() =>
        {
            var mapState = PrepareLoad(server, menuId, partyId);
            new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager)
                .AddGameMenu(OtherMenu, "", _ => Assert.Fail("server opened a stored menu"));
            Campaign.Current.PlayerEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            Campaign.Current.LocationEncounter = ObjectHelper.SkipConstructor<LocationEncounter>();

            mapState.OnLoadingFinished();

            AssertOnMap(mapState);
            Assert.Null(Campaign.Current.PlayerEncounter);
            Assert.Null(Campaign.Current.LocationEncounter);
        });
    }

    [Fact]
    public void ServerLoad_NoStoredMenu_KeepsEncounter()
    {
        var server = environment.Server;
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        server.Call(() =>
        {
            var mapState = PrepareLoad(server, null, partyId);
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            Campaign.Current.PlayerEncounter = encounter;

            mapState.OnLoadingFinished();

            Assert.False(mapState.AtMenu);
            Assert.Same(encounter, Campaign.Current.PlayerEncounter);
        });
    }

    // Nobody plays the server's main hero, so if it is ever captured the captivity menu must not open there:
    // a plain menu zeroes the campaign dt and LateAITickAux then skips PartiesThink for every party.
    [Fact]
    public void ServerTick_CaptiveMainHero_KeepsPartyAiRunning()
    {
        var server = environment.Server;
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var captorId = environment.CreateRegisteredObject<MobileParty>();
        using var partiesThink = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(Campaign), "PartiesThink"));

        server.Call(() =>
        {
            var mapState = PrepareCaptivity(server, partyId, captorId, out var captor);
            TakePrisonerAction.Apply(captor.Party, Hero.MainHero);
            Assert.True(PlayerCaptivity.IsCaptive);

            mapState.OnLoadingFinished();
            TickCampaign();

            Assert.Null(mapState.GameMenuId);
            Assert.False(mapState.AtMenu);
            Assert.True(Campaign.Current._dt > 0f);
            Assert.Equal(1, partiesThink.Count);
        }, CampaignTickDisabledMethods);
    }

    [Fact]
    public void ClientTick_CaptivePlayer_OpensCaptivityMenu()
    {
        var client = environment.Clients.First();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var captorId = environment.CreateRegisteredObject<MobileParty>();

        client.Call(() =>
        {
            var mapState = PrepareCaptivity(client, partyId, captorId, out var captor);
            Campaign.Current.PlayerCaptivity._captorParty = captor.Party;

            mapState.OnLoadingFinished();
            TickCampaign();

            Assert.True(mapState.AtMenu);
            Assert.Equal("menu_captivity_end_no_more_enemies", mapState.MenuContext.GameMenu.StringId);
        }, CampaignTickDisabledMethods);
    }

    // The tick systems around PlayerCaptivity.Update need a full campaign the test doesn't have.
    private static readonly MethodBase[] CampaignTickDisabledMethods =
    {
        AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.Tick)),
        AccessTools.Method(typeof(CampaignPeriodicEventManager), nameof(CampaignPeriodicEventManager.OnTick)),
        AccessTools.Method(typeof(CampaignPeriodicEventManager), nameof(CampaignPeriodicEventManager.TickPeriodicEvents)),
        AccessTools.Method(typeof(CampaignPeriodicEventManager), nameof(CampaignPeriodicEventManager.TickPartialHourlyAi)),
        AccessTools.Method(typeof(CampaignTickCacheDataStore), nameof(CampaignTickCacheDataStore.Tick)),
        AccessTools.Method(typeof(MapEventManager), nameof(MapEventManager.Tick)),
        AccessTools.Method(typeof(EncounterManager), nameof(EncounterManager.Tick)),
    };

    /// <summary>
    /// Loads onto the map with the vanilla captivity menus registered and a captor not at war with the main hero.
    /// PlayerTownVisitCampaignBehavior registers prisoner_wait in game; an empty wait menu is enough here.
    /// </summary>
    private static MapState PrepareCaptivity(EnvironmentInstance instance, string partyId, string captorId, out MobileParty captor)
    {
        var mapState = PrepareLoad(instance, null, partyId);
        Assert.True(instance.ObjectManager.TryGetObject(captorId, out captor));
        captor.IsActive = true;

        var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
        var behavior = new PlayerCaptivityCampaignBehavior();
        behavior.OnSessionLaunched(starter);
        starter.AddWaitGameMenu("prisoner_wait", "", null, null, null, null, GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);
        Campaign.Current.PlayerCaptivity._captivityCampaignBehavior = behavior;
        return mapState;
    }

    private static void TickCampaign()
    {
        // MobilePartyHourlyTick is inlined into Tick, so it can't be disabled; give its ticker nothing to tick.
        Campaign.Current._campaignPeriodicEventManager._mobilePartyHourlyTicker.Initialize(new MBList<MobileParty>(), _ => { }, false);
        Campaign.Current._dt = 0.01f;
        Campaign.Current.CurrentTickCount = 10;
        Campaign.Current.Tick();
        Campaign.Current.LateAITickAux();
    }

    /// <summary>
    /// Builds the state SandBoxGameManager.OnLoadFinished leaves right before it calls OnLoadingFinished:
    /// the vanilla encounter menus registered, the stored menu id on the map state and queued as the next menu.
    /// </summary>
    private static MapState PrepareLoad(EnvironmentInstance instance, string? menuId, string partyId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
        party._currentSettlement = null;
        Campaign.Current.MainParty = party;
        Campaign.Current.PlayerEncounter = null;
        Campaign.Current.MapStateData ??= new MapStateData();

        var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
        new EncounterGameMenuBehavior().OnSessionLaunched(starter);

        var states = Game.Current.GameStateManager;
        var mapState = states.CreateState<MapState>();
        states._gameStates.Add(mapState);
        mapState.GameMenuId = menuId;
        Campaign.Current.GameMenuManager.SetNextMenu(menuId);
        return mapState;
    }

    private static void AssertOnMap(MapState mapState)
    {
        Assert.False(mapState.AtMenu);
        Assert.Null(Campaign.Current.CurrentMenuContext);
        Assert.Null(mapState.GameMenuId);
        Assert.Null(Campaign.Current.GameMenuManager.NextGameMenuId);
    }

    public void Dispose() => environment.Dispose();
}
