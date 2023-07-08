using Common;
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
        private static AllowedInstance<Village> _allowedInstance;
        private static readonly MethodInfo _applyInternal = typeof(ChangeVillageStateAction).GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static);

        public static bool Prefix(Village village, Village.VillageStates newState, MobileParty raiderParty)
        {
            if (village == _allowedInstance?.Instance) return true;

            MessageBroker.Instance.Publish(village, new ChangeVillageState(village.StringId, Convert.ToInt32(newState), raiderParty.StringId));

            return false;
        }

        public static void RunOriginalApplyInternal(Village village, Village.VillageStates newState, MobileParty raiderParty)
        {
            using (_allowedInstance = new AllowedInstance<Village>(village))
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    _applyInternal.Invoke(null, new object[] { village, newState, raiderParty });
                }, true);
            }
        }
    }
}