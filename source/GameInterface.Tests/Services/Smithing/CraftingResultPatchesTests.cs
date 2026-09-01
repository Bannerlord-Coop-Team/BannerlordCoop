using GameInterface.Services.Smithing.Patches;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using Xunit;

namespace GameInterface.Tests.Services.Smithing;

/// <summary>Verifies crafting finalization follows the mode captured by the result popup.</summary>
public sealed class CraftingResultPatchesTests
{
    [Fact]
    public void IsFinalizingOrder_WhenScreenModeChangedAfterCompletion_UsesResultPopupMode()
    {
        var weaponDesign = (WeaponDesignVM)RuntimeHelpers.GetUninitializedObject(
            typeof(WeaponDesignVM));
        var resultPopup = (WeaponDesignResultPopupVM)RuntimeHelpers.GetUninitializedObject(
            typeof(WeaponDesignResultPopupVM));
        resultPopup.IsInOrderMode = true;
        weaponDesign.CraftingResultPopup = resultPopup;
        weaponDesign.IsInOrderMode = false;

        Assert.True(CraftingResultPatches.IsFinalizingOrder(weaponDesign));
    }

    [Fact]
    public void IsFinalizingOrder_WhenResultWasFreeBuild_ReturnsFalse()
    {
        var weaponDesign = (WeaponDesignVM)RuntimeHelpers.GetUninitializedObject(
            typeof(WeaponDesignVM));
        var resultPopup = (WeaponDesignResultPopupVM)RuntimeHelpers.GetUninitializedObject(
            typeof(WeaponDesignResultPopupVM));
        resultPopup.IsInOrderMode = false;
        weaponDesign.CraftingResultPopup = resultPopup;
        weaponDesign.IsInOrderMode = true;

        Assert.False(CraftingResultPatches.IsFinalizingOrder(weaponDesign));
    }
}
