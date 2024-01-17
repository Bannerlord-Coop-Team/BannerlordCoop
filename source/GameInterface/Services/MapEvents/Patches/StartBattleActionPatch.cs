using Common.Messaging;
using Common.Util;
using Common;
using GameInterface.Services.GameDebug.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.MapEvents.Messages;
using System.Diagnostics;
using Common.Logging;
using GameInterface.Services.MobileParties.Handlers;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using System.Collections.Concurrent;
using GameInterface.Services.MobileParties.Extensions;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(EncounterManager))]
    public class StartBattleActionPatch
    {
        [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
        static bool Prefix(PartyBase attackerParty, PartyBase defenderParty)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            
            if (ModInformation.IsClient) return false;

            //Disables interaction between players, this will be handled in a future issue
            if (!attackerParty.MobileParty.IsPartyControlled() && !defenderParty.MobileParty.IsPartyControlled()) { return false; } 

            MessageBroker.Instance.Publish(attackerParty, new BattleStarted(
                attackerParty.MobileParty.StringId,
                defenderParty.MobileParty.StringId));

            return false;
        }

        public static void OverrideOnPartyInteraction(MobileParty interactedParty, MobileParty engagingParty)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using(new AllowedThread())
                {
                    EncounterManager.StartPartyEncounter(engagingParty.Party, interactedParty.Party);
                }
            }, true);
        }
    }
}
