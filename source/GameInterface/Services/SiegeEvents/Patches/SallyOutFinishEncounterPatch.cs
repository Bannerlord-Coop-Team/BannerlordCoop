using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// A fought sally out ends with PlayerEncounter.Finish leaving the besieged settlement, but a client's leave is
/// dropped, so the party stays inside with no encounter. Ask for the settlement encounter again, like vanilla's
/// MenuHelper.EncounterLeaveConsequence does for a party that is still in a settlement.
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish))]
internal class SallyOutFinishEncounterPatch
{
    [HarmonyPrefix]
    private static void Prefix(bool forcePlayerOutFromSettlement, out Settlement __state)
    {
        __state = null;
        if (ModInformation.IsServer || !forcePlayerOutFromSettlement) return;

        // Finish only calls LeaveSettlement for a party that isn't attached to an army
        if (MobileParty.MainParty?.AttachedTo != null) return;

        var mapEvent = PlayerEncounter.Current?._mapEvent;
        if (mapEvent == null) return;
        if (!mapEvent.IsSallyOut && !mapEvent.IsBlockadeSallyOut) return;

        __state = mapEvent.MapEventSettlement;
    }

    [HarmonyPostfix]
    private static void Postfix(Settlement __state)
    {
        if (__state == null) return;
        if (PlayerEncounter.Current != null) return;
        if (PlayerCaptivity.IsCaptive) return;
        if (MobileParty.MainParty?.CurrentSettlement != __state) return;

        EncounterManager.StartSettlementEncounter(MobileParty.MainParty, __state);
    }
}
