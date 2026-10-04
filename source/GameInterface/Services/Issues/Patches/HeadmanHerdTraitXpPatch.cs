using Common;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(TraitLevelingHelper), nameof(TraitLevelingHelper.AddTraitXp))]
internal static class HeadmanHerdTraitXpPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(TraitObject trait, int xpAmount)
    {
        var owner = HeadmanHerdQuestAuthority.TraitOwner;
        if (ModInformation.IsClient || owner == null) return true;
        if (!ContainerProvider.TryResolve<IQuestOwnerTraitXp>(out var traits))
            throw new InvalidOperationException("Quest owner trait XP service is unavailable");
        traits.Apply(owner, trait, xpAmount);
        return false;
    }
}
