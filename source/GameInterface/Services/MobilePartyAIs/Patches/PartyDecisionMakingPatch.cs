using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(AiEngagePartyBehavior))]
internal class AiEngagePartyBehaviorPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<AiEngagePartyBehaviorPatches>();

    [HarmonyPatch(nameof(AiEngagePartyBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }
}

[HarmonyPatch(typeof(AiMilitaryBehavior))]
internal class DisableAiMilitaryBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableAiMilitaryBehavior>();

    [HarmonyPatch(nameof(AiMilitaryBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }
}

[HarmonyPatch(typeof(AiPartyThinkBehavior))]
internal class DisableAiPartyThinkBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableAiPartyThinkBehavior>();

    [HarmonyPatch(nameof(AiPartyThinkBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }
}

[HarmonyPatch(typeof(AiPatrollingBehavior))]
internal class DisableAiPatrollingBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableAiPatrollingBehavior>();

    [HarmonyPatch(nameof(AiPatrollingBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }
}

[HarmonyPatch(typeof(AiVisitSettlementBehavior))]
internal class DisableAiVisitSettlementBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableAiVisitSettlementBehavior>();

    [HarmonyPatch(nameof(AiVisitSettlementBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }
}