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
        private static readonly AllowedInstance<Hero> AllowedInstance = new AllowedInstance<Hero>();

        static bool Prefix(Clan clan, Hero companion)
        {
            if (AllowedInstance.IsAllowed(companion)) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient && clan != Clan.PlayerClan) return false;

            CallStackValidator.Validate(companion, AllowedInstance);

            MessageBroker.Instance.Publish(clan, new CompanionAdded(clan.StringId, companion.StringId));

            return false;
        }

        public static void RunOriginalAddCompanion(Clan clan, Hero companion)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = companion;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    AddCompanionAction.Apply(clan, companion);
                }, true);
            }
        }
    }
}
