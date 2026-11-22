using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

/// <summary>
/// Loads a save whose stored menu is a settlement menu and checks that MapState.OnLoadingFinished
/// no longer runs that menu's init when there is no settlement to show it for.
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
