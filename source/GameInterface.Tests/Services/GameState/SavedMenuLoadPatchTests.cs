using Common.Util;
using GameInterface.Services.GameState.Patches;
using TaleWorlds.CampaignSystem.GameMenus;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

public class SavedMenuLoadPatchTests
{
    // Every menu the crashing EncounterGameMenuBehavior.encounter_menu_ui_castle_on_init is registered for.
    [Theory]
    [InlineData("town_outside")]
    [InlineData("castle_outside")]
    [InlineData("village_outside")]
    [InlineData("castle_guard")]
    [InlineData("fortification_crime_rating")]
    [InlineData("menu_sneak_into_town_succeeded")]
    [InlineData("disguise_first_time")]
    [InlineData("disguise_not_first_time")]
    public void NeedsSettlement_OutsideMenu_IsTrue(string menuId)
    {
        Assert.True(SavedMenuLoadPatch.NeedsSettlement(CreateMenu(menuId, GameMenu.MenuOverlayType.None)));
    }

    [Theory]
    [InlineData(GameMenu.MenuOverlayType.SettlementWithParties)]
    [InlineData(GameMenu.MenuOverlayType.SettlementWithCharacters)]
    [InlineData(GameMenu.MenuOverlayType.SettlementWithBoth)]
    public void NeedsSettlement_SettlementOverlay_IsTrue(GameMenu.MenuOverlayType overlay)
    {
        Assert.True(SavedMenuLoadPatch.NeedsSettlement(CreateMenu("town", overlay)));
    }

    [Theory]
    [InlineData("encounter", GameMenu.MenuOverlayType.Encounter)]
    [InlineData("army_wait", GameMenu.MenuOverlayType.None)]
    [InlineData("menu_settlement_taken", GameMenu.MenuOverlayType.None)]
    public void NeedsSettlement_OtherMenu_IsFalse(string menuId, GameMenu.MenuOverlayType overlay)
    {
        Assert.False(SavedMenuLoadPatch.NeedsSettlement(CreateMenu(menuId, overlay)));
    }

    [Fact]
    public void NeedsSettlement_UnregisteredMenu_IsFalse()
    {
        Assert.False(SavedMenuLoadPatch.NeedsSettlement(null));
    }

    private static GameMenu CreateMenu(string menuId, GameMenu.MenuOverlayType overlay)
    {
        var menu = ObjectHelper.SkipConstructor<GameMenu>();
        menu.StringId = menuId;
        menu.OverlayType = overlay;
        return menu;
    }
}
