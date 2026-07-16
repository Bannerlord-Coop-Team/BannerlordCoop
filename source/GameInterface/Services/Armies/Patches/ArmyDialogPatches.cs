using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.PlayerCaptivityService.Patches;
using HarmonyLib;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerStartCaptivityPatches>();
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
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_encounter_abandon_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixgame_menu_encounter_abandon_on_consequence(EncounterGameMenuBehavior __instance)
    {
        ((PlayerEncounter.Battle != null) ? PlayerEncounter.Battle : PlayerEncounter.EncounteredBattle).BeginWait();
        MobileParty.MainParty.SetMoveModeHold();
        var message = new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, MobileParty.MainParty, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);
        ArmyPatches.RemoveMobilePartyInArmy(MobileParty.MainParty, MobileParty.MainParty.Army, MobileParty.MainParty);
        PlayerEncounter.Finish(true);
        if (MobileParty.MainParty.BesiegerCamp != null)
        {
            MobileParty.MainParty.BesiegerCamp = null; // this will not sync to server/ other clients/ 
        }
        return false;
    }
}
