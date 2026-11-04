using GameInterface.Utils;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace GameInterface.Services.Education.Patches;

/// <summary>
/// Building a stage rolls each option's RandomValue (which picks the available outcomes) and Year8's
/// alternative skills. Seeding per child and stage makes the server and clients build the same stage.
/// </summary>
[HarmonyPatch(typeof(EducationCampaignBehavior), nameof(EducationCampaignBehavior.GetStage), new[] { typeof(Hero), typeof(EducationCampaignBehavior.ChildAgeState) })]
internal static class EducationStageSeedPatch
{
    [HarmonyPrefix]
    private static void Prefix(Hero child, EducationCampaignBehavior.ChildAgeState state, out MBFastRandom __state)
    {
        __state = null;
        if (Game.Current == null || child == null) return;

        __state = Game.Current.RandomGenerator;
        Game.Current.RandomGenerator = new MBFastRandom(StableHash.FromString($"{child.StringId}_{state}"));
    }

    [HarmonyFinalizer]
    private static void Finalizer(MBFastRandom __state)
    {
        if (__state != null && Game.Current != null)
            Game.Current.RandomGenerator = __state;
    }
}
