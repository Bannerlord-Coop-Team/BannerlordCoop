using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using GameInterface.Policies;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(Clan))]
    internal class ClanPatches
    {
        [HarmonyPatch(nameof(Clan.PlayerClan))]
        [HarmonyPatch(MethodType.Getter)]
        [HarmonyPrefix]
        static bool PlayerClanGetter()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            if (Campaign.Current == null) return false;

            return true;
        }
    }
}
