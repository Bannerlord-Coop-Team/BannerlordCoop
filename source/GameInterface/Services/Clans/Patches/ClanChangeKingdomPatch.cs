using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.GameDebug.Patches;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(ChangeKingdomAction), "ApplyInternal")]
    public class ClanChangeKingdomPatch
    {
        static bool Prefix(Clan clan, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, int awardMultiplier = 0, bool byRebellion = false, bool showNotification = true)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient && clan != Clan.PlayerClan) return false;

            MessageBroker.Instance.Publish(clan, new ClanKingdomChanged(clan.StringId, newKingdom?.StringId, (int)detail, awardMultiplier, byRebellion, showNotification));

            return false;
        }

        public static void RunOriginalChangeClanKingdom(Clan clan, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, int awardMultiplier = 0, bool byRebellion = false, bool showNotification = true)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    ChangeKingdomAction.ApplyInternal(clan, newKingdom, detail, awardMultiplier, byRebellion, showNotification);
                }
            }, true);

            
        }
    }
}
