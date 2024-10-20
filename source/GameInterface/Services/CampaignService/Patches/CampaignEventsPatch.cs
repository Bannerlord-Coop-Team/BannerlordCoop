﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CampaignService.Patches
{
    [HarmonyPatch]
    internal class CampaignEventsPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(CampaignEvents).GetMethods()
                .Where(m => m.Name.ToLower().Contains("tick"))
                .Where(m => m.Name.StartsWith("get_") == false);
        }

        public static bool Prefix() => ModInformation.IsServer;
    }

    [HarmonyPatch]
    internal class CampaignEventDispatcherPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(CampaignEventDispatcher).GetMethods()
                .Where(m => m.Name.ToLower().Contains("tick"))
                .Where(m => m.Name.StartsWith("get_") == false)
                .AddItem(AccessTools.Method(typeof(Campaign), nameof(Campaign.Tick)));
        }

        public static bool Prefix() => ModInformation.IsServer;
    }
}
