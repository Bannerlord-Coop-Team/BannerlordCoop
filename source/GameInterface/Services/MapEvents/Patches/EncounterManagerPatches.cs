using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Battles.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches;

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

        var message = new StartSettlementEncounterAttempted(
            attackerParty.StringId,
            settlement.StringId);
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

    [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
    [HarmonyPrefix]
    public static bool Prefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (ModInformation.IsClient) return true;

        var message = new BattleStarted(attackerParty, defenderParty);

        if (attackerParty.MobileParty?.ActualClan?.Name.ToString() == "Playerland")
        {
            InformationManager.DisplayMessage(new InformationMessage($"Local player is engaging in battle with {attackerParty.Name}"));
        }

        MessageBroker.Instance.Publish(null, message);

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.Tick))]
    internal static bool TickPatch(float dt)
    {
        return ModInformation.IsServer;
    }

    internal static void OverrideOnPartyInteraction(PartyBase attacker, PartyBase defender)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (defender.IsMobile)
                {
                    if (attacker.MobileParty.IsPartyControlled() == true)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("Started encounter"));
                    }
                    EncounterManager.StartPartyEncounter(attacker, defender);
                    return;
                }
                if (defender.IsSettlement)
                {
                    EncounterManager.StartSettlementEncounter(attacker.MobileParty, defender.Settlement);
                }
            }
        });
    }
}

