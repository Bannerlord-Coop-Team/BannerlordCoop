using Common.Extensions;
using Common.Messaging;
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
        private static MethodInfo Clan_Name => typeof(Clan).GetProperty(nameof(Clan.Name)).GetSetMethod(true);
        private static MethodInfo Clan_InformalName => typeof(Clan).GetProperty(nameof(Clan.InformalName)).GetSetMethod(true);
        private static readonly Action<Clan, TextObject> Clan_Name_Setter = Clan_Name.BuildDelegate<Action<Clan, TextObject>>();
        private static readonly Action<Clan, TextObject> Clan_InformalName_Setter = Clan_InformalName.BuildDelegate<Action<Clan, TextObject>>();

        static bool Prefix(ref Clan __instance, TextObject name, TextObject informalName)
        {
            MessageBroker.Instance.Publish(__instance, new ChangeClanName(__instance.StringId, name.ToString(), informalName.ToString()));

            return false;
        }
        public static void RunOriginalChangeClanName(Clan clan, TextObject name, TextObject informalName)
        {
            if (clan == null) return;
            Clan_Name_Setter(clan, name);
            Clan_InformalName_Setter(clan, informalName);
        }
    }
}
