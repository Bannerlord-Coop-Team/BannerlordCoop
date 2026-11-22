using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ViewData.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch(typeof(ViewDataTrackerCampaignBehavior), nameof(ViewDataTrackerCampaignBehavior.OnHeroGainedSkill))]
internal class CharacterNotificationPatches
{
    [HarmonyPrefix]
    internal static bool Prefix(ViewDataTrackerCampaignBehavior __instance, Hero hero, out int __state)
    {
        __state = __instance._numOfPerks;

        // Other players manage their own perks; keep vanilla companion notifications.
        return ModInformation.IsServer || !hero.IsPlayerHero() || hero.IsControlledByThisInstance();
    }

    [HarmonyPostfix]
    internal static void Postfix(ViewDataTrackerCampaignBehavior __instance, int __state)
    {
        // Keep the perk notification in the player's saved view data
        if (ModInformation.IsClient && __instance._numOfPerks != __state)
            MessageBroker.Instance.Publish(__instance, new ViewDataChanged(Hero.MainHero));
    }
}
