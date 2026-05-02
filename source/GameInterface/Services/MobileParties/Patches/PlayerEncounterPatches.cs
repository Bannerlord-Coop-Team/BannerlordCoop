using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
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
        if (ModInformation.IsServer) return true;

        if (attackerParty.IsPartyControlled() == false) return false;

        var message = new StartSettlementEncounterAttempted(attackerParty, settlement);
        MessageBroker.Instance.Publish(attackerParty, message);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.HandleEncounterForMobileParty))]
    internal static bool HandleEncounterForMobilePartyPatch(ref MobileParty mobileParty, ref float dt)
    {
        // Skip this method if party is not controlled
        if (mobileParty.IsPartyControlled() == false) return false;

        return true;
    }
}
