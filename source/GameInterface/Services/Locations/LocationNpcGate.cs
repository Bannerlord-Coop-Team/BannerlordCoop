using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations;

/// <summary>
/// Static bridge used by Harmony patches and mission code. State is resolved from the active client's
/// lifetime scope so multiple in-process clients do not share location authority.
/// </summary>
public static class LocationNpcGate
{
    private static readonly ILocationNpcGate Fallback = new LocationNpcGateState();

    public static bool IsCoopLocationMissionActive => Current.IsCoopLocationMissionActive;
    public static string ActiveInstanceId => Current.ActiveInstanceId;
    public static bool IsLocalHostConfirmed => Current.IsLocalHostConfirmed;
    public static bool ShouldSuppressNativeSpawns => Current.ShouldSuppressNativeSpawns;

    public static bool SuppressCapture
    {
        get => Current.SuppressCapture;
        set => Current.SuppressCapture = value;
    }

    public static bool IsReplayingNativePopulation
    {
        get => Current.IsReplayingNativePopulation;
        set => Current.IsReplayingNativePopulation = value;
    }

    public static bool IsPlayerPartyAgent(Agent agent) => IsPlayerPartyAgent(agent, Agent.Main);

    internal static bool IsPlayerPartyAgent(Agent agent, Agent mainAgent) =>
        Current.IsPlayerPartyAgent(agent, mainAgent);

    public static void BeginMission(string instanceId, Func<Agent, bool> partyAgentResolver = null) =>
        Current.BeginMission(instanceId, partyAgentResolver);

    public static void EndMission() => Current.EndMission();

    public static void SetLocalHost(string instanceId, bool isLocalHost) =>
        Current.SetLocalHost(instanceId, isLocalHost);

    private static ILocationNpcGate Current
    {
        get
        {
            return ContainerProvider.TryResolve<ILocationNpcGate>(out var gate)
                ? gate
                : Fallback;
        }
    }
}
