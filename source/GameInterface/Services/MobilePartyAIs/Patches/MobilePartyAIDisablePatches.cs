using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
internal class MobilePartyAIDisablePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAIDisablePatches>();

    [HarmonyPatch(nameof(MobilePartyAi.Tick))]
    [HarmonyPrefix]
    private static bool ClientDisableTickPrefix(MobilePartyAi __instance)
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer || __instance._mobileParty.IsPartyControlled();
    }
}
