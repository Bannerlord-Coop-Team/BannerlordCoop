using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using System;
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

        if (!attackerParty.IsPartyControlled())
            return false;

        var message = new StartSettlementEncounterAttempted(attackerParty, settlement);
        MessageBroker.Instance.Publish(attackerParty, message);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.HandleEncounterForMobileParty))]
    internal static bool HandleEncounterForMobilePartyPatch(ref MobileParty mobileParty, ref float dt)
    {
        // Skip this method if party is not controlled
        if (!mobileParty.IsPartyControlled())
            return false;

        return true;
    }

    [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
    [HarmonyPrefix]
    public static bool PrefixStartPartyEncounter(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (!MapEventConfig.Enabled) return false;

        // Disable player interactions
        if (attackerParty.MobileParty?.IsPlayerParty() == true &&
            defenderParty.MobileParty?.IsPlayerParty() == true) return false;

        return true;
    }

    [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
    [HarmonyPostfix]
    public static void PostfixStartPartyEncounter(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (!MapEventConfig.Enabled) return;

        if (AllowedThread.IsThisThreadAllowed()) return;

        if (ModInformation.IsClient) return;

        var message = new BattleStarted(attackerParty, defenderParty);

        if (attackerParty.MobileParty.IsPlayerParty())
        {
            InformationManager.DisplayMessage(new InformationMessage($"Player is engaging in battle with {attackerParty.Name}"));
        }

        MessageBroker.Instance.Publish(null, message);
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
            try
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
            }
            catch(Exception ex)
            {
                Logger.Error(ex, "Failed to start party encounter");
            }
        });
    }
}

