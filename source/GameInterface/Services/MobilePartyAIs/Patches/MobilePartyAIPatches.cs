using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches
{
    [HarmonyPatch(typeof(MobilePartyAi))]
    internal class MobilePartyAIPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAIPatches>();

        [HarmonyPatch("GetTargetPositionAndFace")]
        [HarmonyPrefix]
        static bool GetTargetPositionAndFace_Fix(ref MobilePartyAi __instance)
        {
            // Maybe fixes crashing on server for null ref exception
            if (__instance._mobileParty == null) return false;
            return true;
        }

        [HarmonyPatch(nameof(MobilePartyAi.CheckPartyNeedsUpdate))]
        [HarmonyPrefix]
        static void Prefix(ref MobilePartyAi __instance)
        {
            if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
            {
                Logger.Error("Unable to resolve {type}\n"
                        + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
                return;
            }

            if (config.IsServer) return;

            if (__instance._mobileParty != MobileParty.MainParty) return;

            EncounterManager.HandleEncounterForMobileParty(__instance._mobileParty, 0f);
        }
    }
}
