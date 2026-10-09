using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.Issues.Framework.Rewards;

/// <summary>
/// Vanilla adds quest trait XP to one store that every player on the server would share. We are skipping this for now.
/// </summary>
[HarmonyPatch(typeof(TraitLevelingHelper))]
internal class QuestTraitXpPatches
{
    [HarmonyPatch(nameof(TraitLevelingHelper.OnIssueSolvedThroughQuest), new Type[] { typeof(Hero), typeof(Tuple<TraitObject, int>[]) })]
    [HarmonyPrefix]
    public static bool OnIssueSolvedThroughQuestPrefix()
    {
        return false;
    }

    [HarmonyPatch(nameof(TraitLevelingHelper.OnIssueSolvedThroughQuest), new Type[] { typeof(Hero), typeof(TraitObject), typeof(int) })]
    [HarmonyPrefix]
    public static bool OnIssueSolvedThroughQuestSingleTraitPrefix()
    {
        return false;
    }

    [HarmonyPatch(nameof(TraitLevelingHelper.OnIssueSolvedThroughAlternativeSolution))]
    [HarmonyPrefix]
    public static bool OnIssueSolvedThroughAlternativeSolutionPrefix()
    {
        return false;
    }

    [HarmonyPatch(nameof(TraitLevelingHelper.OnIssueSolvedThroughBetrayal))]
    [HarmonyPrefix]
    public static bool OnIssueSolvedThroughBetrayalPrefix()
    {
        return false;
    }
}
