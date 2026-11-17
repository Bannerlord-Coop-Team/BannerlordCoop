using Common;
using Common.Messaging;
using Coop.Naval.Notifications.Messages;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using NavalDLC.CharacterDevelopment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalVeteransWisdomCampaignBehaviour))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalVeteransWisdomCampaignBehaviourPatches
{
    [HarmonyPatch(nameof(NavalVeteransWisdomCampaignBehaviour.OnDailyTickParty))]
    [HarmonyPrefix]
    public static bool OnDailyTickPartyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalVeteransWisdomCampaignBehaviour.OnPerkOpened))]
    [HarmonyPrefix]
    public static bool OnPerkOpenedPrefix(NavalVeteransWisdomCampaignBehaviour __instance, Hero hero, PerkObject perk)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var party = hero.PartyBelongedTo;
        if (party != null && hero.IsPlayerHero() && (perk == NavalPerks.Boatswain.NavalHorde || perk == NavalPerks.Boatswain.Optimization || perk == NavalPerks.Boatswain.GildedPurse))
        {
            hero.PartyBelongedTo.ItemRoster.UpdateVersion();

            // Publish message to update roster version on clients
            var message = new UpdateItemRosterVersionAfterPerkChange(hero.PartyBelongedTo.ItemRoster);
            MessageBroker.Instance.Publish(__instance, message);
        }

        return false;
    }
}
