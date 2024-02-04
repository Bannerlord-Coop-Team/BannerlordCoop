using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Reflection;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Common.Extensions;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// Patches the surrender of the player party, only runs on local client
    /// </summary>
    [HarmonyPatch(typeof(PlayerEncounter))]
    public class SurrenderPatch
    {
        private static readonly Action<PlayerEncounter> PlayerSurrenderInternal =
            typeof(PlayerEncounter)
            .GetMethod("PlayerSurrenderInternal", BindingFlags.NonPublic | BindingFlags.Instance)
            .BuildDelegate<Action<PlayerEncounter>>();

        [HarmonyPrefix]
        [HarmonyPatch("PlayerSurrenderInternal")]
        public static bool Prefix()
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            BattleSideEnum enemySide = BattleSideEnum.None;

            if (MobileParty.MainParty.MapEvent.PlayerSide == BattleSideEnum.Defender)
            {
                enemySide = BattleSideEnum.Attacker;
            }
            else if(MobileParty.MainParty.MapEvent.PlayerSide == BattleSideEnum.Attacker)
            {
                enemySide = BattleSideEnum.Defender;
            }

            MessageBroker.Instance.Publish(null, new LocalPlayerSurrendered(
                MobileParty.MainParty.StringId, 
                MobileParty.MainParty.MapEvent.GetMapEventSide(enemySide).LeaderParty.MobileParty.StringId,
                Hero.MainHero.StringId));

            return false;
        }

        public static void RunStartPlayerCaptivity(PartyBase CaptorParty)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    PlayerSurrenderInternal.Invoke(PlayerEncounter.Current);
                }
            });
        }
    }
}