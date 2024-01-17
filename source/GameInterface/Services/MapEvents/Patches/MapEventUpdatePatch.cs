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
        private static readonly MethodInfo MapEvent_FinishBattle = typeof(MapEvent).GetMethod("FinishBattle", BindingFlags.NonPublic | BindingFlags.Instance);

        //[HarmonyPrefix]
        //[HarmonyPatch("Update")]
        //static bool PrefixUpdate(MapEvent __instance) => ModInformation.IsServer;

        [HarmonyPrefix]
        [HarmonyPatch("FinishBattle")]
        static bool PrefixFinishBattle(MapEvent __instance)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (ModInformation.IsClient) return false;

            if (__instance.InvolvedParties.Any(x => !x.MobileParty.IsPartyControlled()))
            {
                return false; //TODO Manage player party interactions
            }

            MobileParty party = __instance.InvolvedParties.First().MobileParty;

            MessageBroker.Instance.Publish(party, new BattleEnded(party.StringId));

            return false;
        }

        public static void OverrideFinishBattle(MapEvent mapEvent)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    MapEvent_FinishBattle.Invoke(mapEvent, new object[] { });
                }
            }, true);
        }
    }
}
