using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for player pressing the leave settlement button.
/// Allows calling the functionality separately from the button press.
/// </summary>
[HarmonyPatch]
internal class PlayerLeaveSettlementPatch
{
    static IEnumerable<MethodBase> TargetMethods() => new MethodInfo[]
    {
        typeof(PlayerTownVisitCampaignBehavior).GetMethod("game_menu_settlement_leave_on_consequence", BindingFlags.NonPublic | BindingFlags.Static),
        typeof(EncounterGameMenuBehavior).GetMethod("game_menu_castle_outside_leave_on_consequence", BindingFlags.NonPublic | BindingFlags.Instance),
        typeof(EncounterGameMenuBehavior).GetMethod("army_encounter_leave_on_consequence", BindingFlags.NonPublic | BindingFlags.Instance),
    };

    private static bool Prefix()
    {
        var party = MobileParty.MainParty;

        var message = new EndSettlementEncounterAttempted(party.StringId);

        MessageBroker.Instance.Publish(party, message);

        return false;
    }
}
