using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(EncounterManager))]
    public class StartBattleActionPatch
    {

        private static string lastAttackerPartyId;

        [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
        static bool Prefix(PartyBase attackerParty, PartyBase defenderParty)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            
            if (ModInformation.IsClient) return false;

            if (lastAttackerPartyId == attackerParty.MobileParty.StringId) return false;
            lastAttackerPartyId = attackerParty.MobileParty.StringId;

            // Disables interaction between players, this will be handled in a future issue
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
