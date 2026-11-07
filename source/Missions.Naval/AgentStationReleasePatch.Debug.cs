#if DEBUG
using Common.Logging;
using GameInterface;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Adds a DEBUG observation to the existing release patch, before its event can trigger another action.</summary>
internal partial class AgentStationReleasePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentStationReleasePatch>();

    /// <summary>Logs the immediate action and hook slot without letting diagnostic failures interrupt station replication.</summary>
    private static void ObserveRelease(Agent agent, UsableMissionObject point)
    {
        try
        {
            if (ContainerProvider.TryResolve<INavalLateHullProbe>(out var probe)) probe.LogRelease(agent, point);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Reading naval station release");
        }
    }
}
#endif
