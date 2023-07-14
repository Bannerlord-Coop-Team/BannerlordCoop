using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Patches
{
    [HarmonyPatch(typeof(ChangeVillageStateAction), "ApplyInternal")]
    public class ChangeVillageStatePatch
    {
        private static AllowedInstance<Village> _allowedInstance = new AllowedInstance<Village>();
        private static MethodInfo _applyInternal => typeof(ChangeVillageStateAction).GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly Action<Village, Village.VillageStates, MobileParty> ApplyInternalDelegate = _applyInternal.BuildDelegate<Action<Village, Village.VillageStates, MobileParty>>();

        public static bool Prefix(Village village, Village.VillageStates newState, MobileParty raiderParty)
        {
            if (_allowedInstance.IsAllowed(village)) return true;

            MessageBroker.Instance.Publish(village, new ChangeVillageState(village.StringId, Convert.ToInt32(newState), raiderParty.StringId));

            return false;
        }

        public static void RunOriginalApplyInternal(Village village, Village.VillageStates newState, MobileParty raiderParty)
        {
            using (_allowedInstance)
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    _allowedInstance.Instance = village;
                    ApplyInternalDelegate(village, newState, raiderParty);
                }, true);
            }
        }
    }
}