using Common.Messaging;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

/// <summary>
/// Patch for Joining army through the EncounterGameMenu
/// Can instantly call AddPartyToMergedParties since,
/// the player stays near the leaderparty
/// </summary>
namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch]
internal class ArmyDialogPatches
{
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_army_join_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefix(EncounterGameMenuBehavior __instance, MenuCallbackArgs args)
    {
        using (new AllowedThread())
        {
            ArmyPatches.AddMobilePartyInArmy(MobileParty.MainParty, PlayerEncounter.EncounteredMobileParty.Army);
            MobileParty.MainParty.Army.AddPartyToMergedParties(MobileParty.MainParty);
        }
        var message = new MobilePartyInArmyAdded(PlayerEncounter.EncounteredMobileParty.Army, MobileParty.MainParty, true);
        MessageBroker.Instance.Publish(__instance, message);
        PlayerEncounter.Finish(true);
        return false;
    }
}
