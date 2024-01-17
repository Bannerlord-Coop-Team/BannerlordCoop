using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEvent))]
    public class SimulateBattlePatch
    {

        //All code here can be used IF we decide to sync the simulation instead of the TroopCount, XP etc.
        //It is not fully functional but the only "random" thing it does that I can find is in SimulateForBattleRound
        //Thus should be 1:1 Input-Output if we sync that call

        private static readonly Action<MapEvent, BattleSideEnum, float> MapEvent_SimulateBattleRound =
            typeof(MapEvent)
            .GetMethod("SimulateBattleForRound", BindingFlags.NonPublic | BindingFlags.Instance)
            .BuildDelegate<Action<MapEvent, BattleSideEnum, float>>();

        //[HarmonyPrefix]
        //[HarmonyPatch("SimulateBattleForRound")]
        //static bool Prefix(MapEvent __instance, BattleSideEnum side, float advantage)
        //{
        //    if (AllowedThread.IsThisThreadAllowed()) return true;

        //    if (ModInformation.IsClient) return false;

        //    MessageBroker.Instance.Publish(__instance, new BattleRoundSimulated(
        //        __instance.DefenderSide.Parties[0].Party.MobileParty.StringId,
        //        (int)side,
        //        advantage
        //        ));

        //    return false;
        //}

        public static void OverrideSimulateBattleRound(MapEvent mapEvent, BattleSideEnum side, float advantage)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    if(mapEvent == null) return;
                    MapEvent_SimulateBattleRound.Invoke(mapEvent, side, advantage);
                }
            }, true);
        }
    }
}