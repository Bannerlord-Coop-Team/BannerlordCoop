using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Diagnostics;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Parties are always visible on server
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsSpotted))]
internal class PartyIsSpottedServerPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyVisibilityOnServerPatch>();

    private static void Postfix(ref bool __result)
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return;
        }

        if (config.IsServer || Debugger.IsAttached)
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(MobileParty))]
internal class PartyVisibilityOnServerPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyVisibilityOnServerPatch>();

    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Setter)]
    private static void Prefix(ref bool value)
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return;
        }

        if (config.IsServer || Debugger.IsAttached)
        {
            value = true;
        }
    }

    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Getter)]
    private static void Postfix(ref bool __result)
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return;
        }

        if (config.IsServer || Debugger.IsAttached)
        {
            __result = true;
        }
    }
}