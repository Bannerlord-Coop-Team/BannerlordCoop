using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.GameDebug.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(AddCompanionAction), "ApplyInternal")]
    public class ClanAddCompanionPatch
    {
        static bool Prefix(Clan clan, Hero companion)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient && clan != Clan.PlayerClan) return false;

            MessageBroker.Instance.Publish(clan, new CompanionAdded(clan.StringId, companion.StringId));

            return false;
        }

        public static void RunOriginalAddCompanion(Clan clan, Hero companion)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using(new AllowedThread())
                {
                    AddCompanionAction.Apply(clan, companion);
                }
            }, true);
        }
    }
}