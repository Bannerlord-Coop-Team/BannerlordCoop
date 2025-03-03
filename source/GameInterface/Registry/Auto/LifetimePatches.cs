using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Registry.Auto;
internal class LifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches>();

    internal static bool Prefix<T>(ref T __instance) where T : class
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return true;
        }

        var message = new InstanceCreated<T>(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}