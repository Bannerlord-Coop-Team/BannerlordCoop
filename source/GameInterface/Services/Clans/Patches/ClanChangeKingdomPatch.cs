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
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        private static readonly Action<Clan, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, int, bool, bool> ApplyInternal =
            typeof(ChangeKingdomAction)
            .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
            .BuildDelegate<Action<Clan, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, int, bool, bool>>();

        static bool Prefix(Clan clan, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, int awardMultiplier = 0, bool byRebellion = false, bool showNotification = true)
        {
            if (AllowedInstance.IsAllowed(clan)) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient && clan != Clan.PlayerClan) return false;

            CallStackValidator.Validate(clan, AllowedInstance);

            MessageBroker.Instance.Publish(clan, new ClanKingdomChanged(clan.StringId, newKingdom?.StringId, (int)detail, awardMultiplier, byRebellion, showNotification));

            return false;
        }

        public static void RunOriginalChangeClanKingdom(Clan clan, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, int awardMultiplier = 0, bool byRebellion = false, bool showNotification = true)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = clan;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ApplyInternal.Invoke(clan, newKingdom, detail, awardMultiplier, byRebellion, showNotification);
                }, true);
            }
        }
    }
}
