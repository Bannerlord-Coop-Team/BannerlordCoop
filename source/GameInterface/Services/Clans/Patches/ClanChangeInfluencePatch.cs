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
    [HarmonyPatch(typeof(ChangeClanInfluenceAction), "ApplyInternal")]
    public class ClanChangeInfluencePatch
    {
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        static bool Prefix(Clan clan, float amount)
        {
            if (AllowedInstance.IsAllowed(clan)) return true;

            // If not controlled by client skip call
            if (ModInformation.IsClient && clan != Clan.PlayerClan) return false;

            if (PolicyProvider.AllowOriginalCalls) return true;

            //CallStackValidator.Validate(clan, AllowedInstance);

            MessageBroker.Instance.Publish(clan, new ClanInfluenceChanged(clan.StringId, amount));

            return false;
        }

        public static void RunOriginalChangeClanInfluence(Clan clan, float amount)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = clan;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ChangeClanInfluenceAction.Apply(clan, amount);
                }, true);
            }
        }
    }
}
