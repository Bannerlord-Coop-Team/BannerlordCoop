using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using static TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction;

namespace GameInterface.Services.Kingdoms.Patches
{
    [HarmonyPatch(typeof(ChangeKingdomAction), "ApplyInternal")]
    public class ChangeKingdomActionPatch
    {
        private readonly static AllowedInstance<Clan> _allowedInstance = new AllowedInstance<Clan>();
        private static readonly MethodInfo _applyInternal = typeof(ChangeKingdomAction).GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static);

        public static bool Prefix(Clan clan, Kingdom newKingdom, ChangeKingdomActionDetail detail, 
            int awardMultiplier = 0, bool byRebellion = false, bool showNotification = true)
        {
            if (_allowedInstance.IsAllowed(clan)) return true;

            MessageBroker.Instance.Publish(clan, new UpdateKingdomRelation(clan, newKingdom, Convert.ToInt32(detail), awardMultiplier, byRebellion, showNotification));

            return false;
        }

        public static void RunOriginalApplyInternal(Clan clan, Kingdom newKingdom, ChangeKingdomActionDetail detail,
            int awardMultiplier = 0, bool byRebellion = false, bool showNotification = true)
        {
            using (_allowedInstance)
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    _allowedInstance.Instance = clan;
                    _applyInternal.Invoke(null, new object[] { clan, newKingdom, detail, awardMultiplier, byRebellion, showNotification });
                }, true);
            }
        }
    }
}
