using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for player encounters
/// </summary>

[HarmonyPatch(typeof(EncounterManager))]
internal class EncounterManagerPatches
{
    private static ILogger Logger = LogManager.GetLogger<EncounterManagerPatches>();

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.StartSettlementEncounter))]
    private static bool Prefix(MobileParty attackerParty, Settlement settlement)
    {
        Logger.Information(
            "EncounterManager.StartSettlementEncounter intercept party={partyId} settlement={settlementId} partyNull={partyNull} settlementPartyNull={settlementNull} current={current}",
            attackerParty.StringId,
            settlement.StringId,
            attackerParty.Party == null,
            settlement.Party == null,
            attackerParty.CurrentSettlement?.StringId ?? "none");
        // Toujours laisser l'original se charger pour éviter de créer l'UI avant que UIContext soit prêt
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.HandleEncounterForMobileParty))]
    internal static bool HandleEncounterForMobilePartyPatch(ref MobileParty mobileParty)
    {
        Logger.Information(
            "EncounterManager.HandleEncounterForMobileParty intercept party={partyId} partyNull={partyNull} current={current}",
            mobileParty.StringId,
            mobileParty.Party == null,
            mobileParty.CurrentSettlement?.StringId ?? "none");
        // Toujours laisser l'original pour éviter de perturber les autres parties
        return true;
    }
}
