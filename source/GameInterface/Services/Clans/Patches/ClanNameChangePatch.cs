using Autofac;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient && __instance != Clan.PlayerClan) return false;

            MessageBroker.Instance.Publish(__instance, new ClanNameChanged(__instance.StringId, name.ToString(), informalName.ToString()));

            return false;
        }
        public static void RunOriginalChangeClanName(Clan clan, TextObject name, TextObject informalName)
        {
            if (clan == null) return;
            clan.Name = name;
        }
    }
}
