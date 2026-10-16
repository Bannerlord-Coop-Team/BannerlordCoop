using Common;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch(typeof(ViewDataTrackerCampaignBehavior), nameof(ViewDataTrackerCampaignBehavior.OnHeroGainedSkill))]
internal class CharacterNotificationPatches
{
    [HarmonyPrefix]
    internal static bool Prefix(Hero hero)
    {
        // Other players manage their own perks; keep vanilla companion notifications.
        return ModInformation.IsServer || !hero.IsPlayerHero() || hero.IsControlledByThisInstance();
    }
}
