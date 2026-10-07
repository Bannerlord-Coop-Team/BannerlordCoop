using GameInterface.Services.Clans.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

/// <summary>
/// The v1.5 governor trait effects apply only while the governor is in town. Each transpiler must swap the
/// method's single governor CurrentSettlement read, so a player governor counts as present in their town.
/// </summary>
public class CoopClanGovernorPatchesTests
{
    [Theory]
    [InlineData(nameof(CoopClanGovernorPatches.GovernorBoostCostTranspiler))]
    [InlineData(nameof(CoopClanGovernorPatches.GovernorFoodTranspiler))]
    [InlineData(nameof(CoopClanGovernorPatches.GovernorLoyaltyTranspiler))]
    [InlineData(nameof(CoopClanGovernorPatches.GovernorWorkshopTranspiler))]
    [InlineData(nameof(CoopClanGovernorPatches.GovernorHearthTranspiler))]
    [InlineData(nameof(CoopClanGovernorPatches.GovernorRebellionTranspiler))]
    public void GovernorTraitEffect_ReadsThePlayerGovernorsTown(string transpilerName)
    {
        var transpiler = AccessTools.Method(typeof(CoopClanGovernorPatches), transpilerName);
        var patch = transpiler.GetCustomAttributes<HarmonyPatch>().Single().info;
        var target = AccessTools.Method(patch.declaringType, patch.methodName);
        Assert.NotNull(target);

        var heroSettlement = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.CurrentSettlement));
        var staticSettlement = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.CurrentSettlement));
        var governorSettlement = AccessTools.Method(typeof(CoopClanGovernorPatches), nameof(CoopClanGovernorPatches.GetGovernorSettlement));

        var original = PatchProcessor.GetOriginalInstructions(target).ToList();
        var patched = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { original.Select(code => code.Clone()).ToList() })).ToList();

        Assert.Single(original.Where(code => code.Calls(heroSettlement)));
        if (transpilerName == nameof(CoopClanGovernorPatches.GovernorRebellionTranspiler))
            Assert.Single(original.Where(code => code.Calls(staticSettlement)));
        Assert.DoesNotContain(patched, code => code.Calls(heroSettlement));
        Assert.Single(patched.Where(code => code.Calls(governorSettlement)));
        Assert.Equal(
            original.Count(code => code.Calls(staticSettlement)),
            patched.Count(code => code.Calls(staticSettlement)));
    }
}
