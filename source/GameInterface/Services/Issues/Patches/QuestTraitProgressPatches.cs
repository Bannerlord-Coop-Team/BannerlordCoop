using GameInterface.Policies;
using GameInterface.Services.Issues.Handlers;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(TraitLevelingHelper), "AddPlayerTraitXPAndLogEntry")]
internal class QuestTraitProgressPatch
{
    [HarmonyPrefix]
    private static bool Prefix(TraitObject trait, int xpValue, ActionNotes context, Hero referenceHero)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return !ContainerProvider.TryResolve<QuestTraitProgressHandler>(out var progress)
            || !progress.TryApply(trait, xpValue, context, referenceHero);
    }
}

[HarmonyPatch(typeof(Campaign), nameof(Campaign.OnPlayerCharacterChanged))]
internal class QuestTraitProgressCharacterPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ContainerProvider.TryResolve<QuestTraitProgressHandler>(out var progress)) progress.RestoreLocalProgress();
    }
}
