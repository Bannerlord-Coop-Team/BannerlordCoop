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
        static bool Prefix(Clan clan, float amount)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (amount == 0f) return false;

            // If not controlled by client skip call
            if (ModInformation.IsClient && clan != Clan.PlayerClan) return false;

            MessageBroker.Instance.Publish(clan, new ClanInfluenceChanged(clan.StringId, amount));

            return false;
        }

        public static void RunOriginalChangeClanInfluence(Clan clan, float amount)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    ChangeClanInfluenceAction.Apply(clan, amount);
                }
            });
        }
    }
}
