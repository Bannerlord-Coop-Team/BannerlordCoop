using GameInterface.Services.Clans.Patches;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

/// <summary>
/// The governor trait effects apply only while the governor is in town. The transpiler must swap every governor
/// CurrentSettlement read of each target, so a player governor counts as present in their town.
/// </summary>
public class GovernorSettlementPatchesTests
{
    public static IEnumerable<object[]> Targets() =>
        GovernorSettlementPatches.TargetMethods().Select(method => new object[] { method });

    [Theory]
    [MemberData(nameof(Targets))]
    public void GovernorEffect_ReadsThePlayerGovernorsTown(MethodBase target)
    {
        Assert.NotNull(target);

        var heroSettlement = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.CurrentSettlement));
        var staticSettlement = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.CurrentSettlement));
        var governorSettlement = AccessTools.Method(typeof(HeroExtensions), nameof(HeroExtensions.GetGovernorSettlement));

        var original = PatchProcessor.GetOriginalInstructions(target).ToList();
        var patched = GovernorSettlementPatches.Transpiler(original.Select(code => code.Clone()).ToList()).ToList();

        int heroReads = original.Count(code => code.Calls(heroSettlement));
        Assert.True(heroReads > 0, $"{target.DeclaringType.Name}.{target.Name} no longer reads Hero.CurrentSettlement");
        Assert.DoesNotContain(patched, code => code.Calls(heroSettlement));
        Assert.Equal(heroReads, patched.Count(code => code.Calls(governorSettlement)));
        Assert.Equal(
            original.Count(code => code.Calls(staticSettlement)),
            patched.Count(code => code.Calls(staticSettlement)));
    }
}
