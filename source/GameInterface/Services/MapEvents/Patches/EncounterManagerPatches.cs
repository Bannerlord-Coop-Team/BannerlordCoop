using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
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
    [HarmonyPatch(nameof(EncounterManager.Tick))]
    internal static bool TickPatch(float dt)
    {
        return ModInformation.IsServer;
    }

    internal static void OverrideOnPartyInteraction(MobileParty attacker, PartyBase defender)
    {
        using(new AllowedThread())
        {
            if (defender.IsMobile)
            {
                if(attacker.IsPartyControlled() == true)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Started encounter"));
                }
                defender.MobileParty.OnPartyInteraction(attacker);
                return;
            }
            if (defender.IsSettlement)
            {
                defender.Settlement.OnPartyInteraction(attacker);
            }
        }
    }
}
