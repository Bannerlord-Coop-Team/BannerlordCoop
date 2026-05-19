//using Common.Logging;
//using Common.Messaging;
//using GameInterface.Services.MapEvents.Messages;
//using HarmonyLib;
//using Helpers;
//using Serilog;
//using TaleWorlds.CampaignSystem.Encounters;
//using TaleWorlds.CampaignSystem.MapEvents;
//using TaleWorlds.CampaignSystem.Party;
//using TaleWorlds.Core;

//namespace GameInterface.Services.MapEvents.Patches;

//[HarmonyPatch(typeof(MenuHelper))]
//public class PlayerEncounterFinalizePatch
//{
//    private static readonly ILogger Logger = LogManager.GetLogger<PlayerEncounterFinalizePatch>();

//    [HarmonyPrefix]
//    [HarmonyPatch(nameof(MenuHelper.EncounterLeaveConsequence))]
//    static bool PrefixLeaveBattle() //TODO Sync player battle results
//    {
//        MessageBroker.Instance.Publish(MobileParty.MainParty, new LeaveBattleAttempted(MobileParty.MainParty, MapEvent.PlayerMapEvent));

//        return false;
//    }
//}