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
using System.Runtime.CompilerServices;
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
        if (AllowedThread.IsThisThreadAllowed()) return true;

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
    [HarmonyPrefix]
    public static bool PrefixStartPartyEncounter(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (ModInformation.IsServer) return true;

        var message = new BattleStarted(attackerParty, defenderParty);

        if (attackerParty.MobileParty.IsPlayerParty())
        {
            InformationManager.DisplayMessage(new InformationMessage($"Player is engaging in battle with {attackerParty.Name}"));
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
}

