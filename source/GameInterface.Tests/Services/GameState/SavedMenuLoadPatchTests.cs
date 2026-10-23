using Common.Util;
using GameInterface.Services.GameState.Patches;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
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

    // A postfix runs after EnterMenuMode has already run the menu init, which is where the load crashed.
    [Fact]
    public void Patch_RunsBeforeOnLoadingFinished()
    {
        var patches = typeof(SavedMenuLoadPatch)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.GetCustomAttributes<HarmonyPatch>()
                .Any(patch => patch.info.declaringType == typeof(MapState) &&
                    patch.info.methodName == nameof(MapState.OnLoadingFinished)))
            .ToArray();

        var patch = Assert.Single(patches);
        Assert.NotNull(patch.GetCustomAttribute<HarmonyPrefix>());
    }

    private static GameMenu CreateMenu(string menuId, GameMenu.MenuOverlayType overlay)
    {
        var menu = ObjectHelper.SkipConstructor<GameMenu>();
        menu.StringId = menuId;
        menu.OverlayType = overlay;
        return menu;
    }
}
