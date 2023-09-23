using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(Clan), nameof(Clan.ChangeClanName))]
    public class ClanNameChangePatch
    {
        private static MethodInfo Clan_Name = typeof(Clan).GetProperty(nameof(Clan.Name)).GetSetMethod(true);
        private static readonly Action<Clan, TextObject> Clan_Name_Setter = Clan_Name.BuildDelegate<Action<Clan, TextObject>>();

        static bool Prefix(ref Clan __instance, TextObject name, TextObject informalName)
        {
            MessageBroker.Instance.Publish(__instance, new ClanNameChange(__instance, name.ToString(), informalName.ToString()));

            return false;
        }
        public static void RunOriginalChangeClanName(Clan clan, TextObject name, TextObject informalName)
        {
            if (clan == null) return;
            Clan_Name_Setter(clan, name);
        }
    }
}
