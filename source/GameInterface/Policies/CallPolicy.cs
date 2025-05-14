using Autofac;
using Common.Logging;
using Common.Util;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Policies;

public class CallPolicy
{
    private static readonly ILogger Logger = LogManager.GetLogger<CallPolicy>();

    public static bool IsOriginalAllowed()
    {
        // While using allowed thread, allow original call
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (ContainerProvider.TryResolve<ISyncPolicy>(out var syncPolicy) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(ISyncPolicy));
            return true;
        }

        if (syncPolicy.AllowOriginal()) return true;

        return false;
    }

    public static bool SkipIfClient(ILogger logger, out bool result)
    {
        result = false;

        // Invalid setup, do nothing with the result
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false) return false;

        if (config.IsClient)
        {
            Logger.Error("Attempted to run server code on client\n"
                    + "Callstack: {callstack}", Environment.StackTrace);

            // Allow original to prevent crashing, logging should be enough to diagnose
            result = true;
            return true;
        }

        // Not client, do nothing with the result
        return false;
    }

    public static bool SkipIfServer(ILogger logger, out bool result)
    {
        result = true;

        // Invalid setup, do nothing with the result
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false) return false;

        if (config.IsServer) return true;

        // Not client, do nothing with the result
        return false;
    }
}
