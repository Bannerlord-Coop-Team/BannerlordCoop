using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Traits.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.Traits.Patches;

[HarmonyPatch(typeof(TraitLevelingHelper))]
internal class TraitLevelingHelperPatches
{
    [HarmonyPatch(nameof(TraitLevelingHelper.AddPlayerTraitXPAndLogEntry))]
    [HarmonyPrefix]
    public static bool AddPlayerTraitXPAndLogEntryPrefix(TraitObject trait, int xpValue, ActionNotes context, Hero referenceHero)
    {
        if (ModInformation.IsClient) return false;

        // Callers set the player hero through MainHeroSubstitutionScope
        var playerHero = ResolvedMainHeroContext.ResolvedMainHero;
        if (playerHero == null) return false;

        if (!ContainerProvider.TryResolve<ITraitLevelingHelperInterface>(out var traitLevelingHelperInterface)) return false;

        traitLevelingHelperInterface.AddTraitXp(playerHero, trait, xpValue, context, referenceHero);

        return false;
    }
}
