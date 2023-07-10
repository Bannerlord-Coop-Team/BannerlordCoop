using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for player pressing the leave settlement button.
/// Allows calling the functionality separately from the button press.
/// </summary>
[HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior))]
internal class PlayerLeaveSettlementPatch
{
    [HarmonyPatch("game_menu_settlement_leave_on_consequence")]
    private static bool Prefix()
    {
        var party = MobileParty.MainParty;

        var message = new EndSettlementEncounterAttempted(party.StringId);

        MessageBroker.Instance.Publish(party, message);

        return false;
    }

    public static void OverrideLeaveConsequence()
    {
        using (LeaveSettlementActionPatches.AllowedInstance)
        {
            LeaveSettlementActionPatches.AllowedInstance.Instance = MobileParty.MainParty;
            PlayerEncounter.LeaveSettlement();
            PlayerEncounter.Finish(true);
            Campaign.Current.SaveHandler.SignalAutoSave();
        }
    }
}
