using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.StanceLinks.Messages.Data;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.StanceLinks.Messages;

namespace GameInterface.Services.StanceLinks.Patches
{
    [HarmonyPatch(typeof(StanceLink))]
    internal class StanceLinkDataPatches
    {
        static readonly ILogger Logger = LogManager.GetLogger<StanceLinkDataPatches>();

        [HarmonyPatch(nameof(StanceLink.Faction1))]
        [HarmonyPatch(MethodType.Setter)]
        [HarmonyPrefix]
        public static bool PrefixFaction1Setter(ref StanceLink __instance, ref IFaction value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return true;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client tried to modify Faction1 in object {name}\n"
                + "Callstack: {callstack}", typeof(StanceLink), Environment.StackTrace);
            }

            MessageBroker.Instance.Publish(__instance, new StanceLinkFactionChanged(__instance, value, true));
            return true;
        }


        [HarmonyPatch(nameof(StanceLink.Faction2))]
        [HarmonyPatch(MethodType.Setter)]
        [HarmonyPrefix]
        public static bool PrefixFaction2Setter(ref StanceLink __instance, ref IFaction value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return true;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client tried to modify Faction2 in object {name}\n"
                + "Callstack: {callstack}", typeof(StanceLink), Environment.StackTrace);
            }

            MessageBroker.Instance.Publish(__instance, new StanceLinkFactionChanged(__instance, value, false));
            return true;
        }
    }
}
