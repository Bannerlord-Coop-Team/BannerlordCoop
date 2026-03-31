using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Battles.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// Patches the StartBattle in PlayerEncounter, only runs on local client
    /// </summary>
    [HarmonyPatch(typeof(PlayerEncounter))]
    public class PlayerEncounterPatch
    {
        [HarmonyPatch("StartBattleInternal")]
        [HarmonyPrefix]
        public static bool Prefix(ref PlayerEncounter __instance)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (ModInformation.IsServer) return true;

            var message = new PlayerStartBattle();

            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }
    }

    [HarmonyPatch(typeof(PartyBase))]
    public class TestPatching2
    {
        [HarmonyPatch("TaleWorlds.CampaignSystem.Map.IInteractablePoint.OnPartyInteraction")]
        [HarmonyPrefix]
        public static bool Prefix(PartyBase __instance, MobileParty engagingParty)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            var message = new BattleStarted(engagingParty, __instance);

            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }
    }
}