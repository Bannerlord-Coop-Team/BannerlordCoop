using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<StartBattleHandler>();
        private static readonly AllowedInstance<MapEvent> AllowedInstance = new AllowedInstance<MapEvent>();

        private static MobileParty lastMobileParty;

        private static readonly MethodInfo MapEvent_FinishBattle = typeof(MapEvent).GetMethod("FinishBattle", BindingFlags.NonPublic | BindingFlags.Instance);


        //[HarmonyPrefix]
        //[HarmonyPatch("ApplyBattleResults")] //Make sure client cannot give itself rewards (Maybe needed?)
        //static bool PrefixApplyBattleResults()
        //{
        //    if (ModInformation.IsClient) return false;
        //    return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        static bool PrefixUpdate(MapEvent __instance)
        {
            if (ModInformation.IsClient) return false;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("FinishBattle")]
        static bool PrefixFinishBattle(MapEvent __instance)
        {
            if (AllowedInstance.IsAllowed(__instance)) 
            {
                if(ModInformation.IsClient)
                {
                    Logger.Information("Ended battle on client: " + __instance.ToString());
                }
                else
                {
                    Logger.Information("Ended battle on server: " + __instance.ToString());
                }
                return true;
            }

            if (ModInformation.IsClient) return false;

            if (__instance.InvolvedParties.Any(x => !x.MobileParty.IsPartyControlled()))
            {
                return false; //Player involved
            }

            MobileParty party = __instance.InvolvedParties.First().MobileParty;

            lastMobileParty = party;
            MessageBroker.Instance.Publish(party, new BattleEnded(party.StringId));

            return false;
        }

        public static void RunOriginalFinishBattle(MapEvent mapEvent)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = mapEvent;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    if (mapEvent == null) return;
                    MapEvent_FinishBattle.Invoke(mapEvent, null);
                }, true);
            }
        }
    }
}
