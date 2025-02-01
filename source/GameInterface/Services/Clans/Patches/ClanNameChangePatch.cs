using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(Clan), nameof(Clan.ChangeClanName))]
    public class ClanNameChangePatch
    {
        private static ILogger Logger = LogManager.GetLogger<ClanNameChangePatch>();

        static bool Prefix(ref Clan __instance, TextObject name, TextObject informalName)
        {
            if(AllowedThread.IsThisThreadAllowed()) return true;

            if(Campaign.Current.MainParty.ActualClan == __instance)
            {
                MessageBroker.Instance.Publish(null, new ClanNameChanged(Campaign.Current.MainParty.ActualClan.StringId, name.ToString(), informalName.ToString()));
                return false;
            }

            if (ModInformation.IsServer) return true;

            return true;
        }

        public static void RunOriginalChangeClanName(Clan clan, TextObject name, TextObject informalName)
        {
            if (clan == null) return;

            using (new AllowedThread())
            {
                clan.ChangeClanName(name, informalName);
            }
        }
    }
}