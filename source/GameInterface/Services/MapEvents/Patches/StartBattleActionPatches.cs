using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.MapEvents.MapEvent;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(StartBattleAction))]
internal class StartBattleActionPatches
{
    [HarmonyPatch(nameof(StartBattleAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool PrefixApply(PartyBase attackerParty, PartyBase defenderParty, object subject, MapEvent.BattleTypes battleType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            var requestBattleMessgae = new StartBattleAttempted(attackerParty, defenderParty, subject as Settlement, battleType);
            MessageBroker.Instance.Publish(null, requestBattleMessgae);
            return false;
        }

        return true;
    }
}

//[HarmonyPatch(typeof(DefaultEncounterModel))]
//internal class DefaultEncounterModelPatches
//{
//    [HarmonyPatch(nameof(DefaultEncounterModel.CreateMapEventComponentForEncounter))]
//    [HarmonyPostfix]
//    public static void PostfixCreateMapEventComponentForEncounter(PartyBase attackerParty, PartyBase defenderParty, MapEventComponent __result)
//    {
//        if (CallOriginalPolicy.IsOriginalAllowed()) return;

//        if (ModInformation.IsServer)
//        {
//            var startBattleMessage = new BattleStarted(__result.MapEvent, attackerParty, defenderParty);
//            MessageBroker.Instance.Publish(null, startBattleMessage);
//        }
//    }
//}
