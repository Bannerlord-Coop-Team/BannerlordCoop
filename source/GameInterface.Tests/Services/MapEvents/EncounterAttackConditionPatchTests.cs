using Common.Util;
using GameInterface.Services.MapEvents.Patches;
using TaleWorlds.CampaignSystem.GameMenus;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class EncounterAttackConditionPatchTests
{
    // R37: on the post-battle encounter menu vanilla set no tooltip, and the wounded entry wrote into the missing one.
    [Fact]
    public void AllowWoundedAttack_WithoutAVanillaTooltip_EnablesTheOptionWithItsOwnTooltip()
    {
        var args = ObjectHelper.SkipConstructor<MenuCallbackArgs>();
        args.IsEnabled = false;

        EncounterAttackConditionPatch.AllowWoundedAttack(args);

        Assert.True(args.IsEnabled);
        Assert.NotNull(args.Tooltip);
    }
}
