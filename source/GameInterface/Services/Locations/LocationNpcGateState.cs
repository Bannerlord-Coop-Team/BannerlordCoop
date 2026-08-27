using Common.Logging;
using Serilog;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations;

public interface ILocationNpcGate
{
    bool IsCoopLocationMissionActive { get; }
    string ActiveInstanceId { get; }
    bool IsLocalHostConfirmed { get; }
    bool ShouldSuppressNativeSpawns { get; }
    bool SuppressCapture { get; set; }
    bool IsReplayingNativePopulation { get; set; }

    bool IsPlayerPartyAgent(Agent agent, Agent mainAgent);
    void BeginMission(string instanceId, Func<Agent, bool> partyAgentResolver = null);
    void EndMission();
    void SetLocalHost(string instanceId, bool isLocalHost);
}

/// <summary>
/// Per-client state for the active settlement location mission and its confirmed NPC authority.
/// </summary>
public class LocationNpcGateState : ILocationNpcGate, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationNpcGateState>();

    private readonly object stateLock = new object();
    private readonly ThreadLocal<bool> suppressCapture = new ThreadLocal<bool>();
    private readonly ThreadLocal<bool> isReplayingNativePopulation = new ThreadLocal<bool>();
    private string activeInstanceId;
    private bool localHostConfirmed;
    private Func<Agent, bool> partyAgentResolver;

    public bool IsCoopLocationMissionActive
    {
        get { lock (stateLock) return activeInstanceId != null; }
    }

    public string ActiveInstanceId
    {
        get { lock (stateLock) return activeInstanceId; }
    }

    public bool IsLocalHostConfirmed
    {
        get { lock (stateLock) return localHostConfirmed; }
    }

    public bool ShouldSuppressNativeSpawns
    {
        get { lock (stateLock) return activeInstanceId != null && !localHostConfirmed; }
    }

    public bool SuppressCapture
    {
        get => suppressCapture.Value;
        set => suppressCapture.Value = value;
    }

    public bool IsReplayingNativePopulation
    {
        get => isReplayingNativePopulation.Value;
        set => isReplayingNativePopulation.Value = value;
    }

    public bool IsPlayerPartyAgent(Agent agent, Agent mainAgent)
    {
        if (agent == null) return false;
        if (ReferenceEquals(agent, mainAgent)) return true;

        if (agent.Origin is PartyAgentOrigin origin)
        {
            PartyBase mainParty = PartyBase.MainParty;
            if (mainParty != null && origin.Party == mainParty)
                return true;
        }

        Func<Agent, bool> resolver;
        lock (stateLock) resolver = partyAgentResolver;
        return resolver?.Invoke(agent) == true;
    }

    public void BeginMission(string instanceId, Func<Agent, bool> partyAgentResolver = null)
    {
        if (string.IsNullOrEmpty(instanceId)) throw new ArgumentException("instanceId is required", nameof(instanceId));

        lock (stateLock)
        {
            if (activeInstanceId != null && activeInstanceId != instanceId)
            {
                Logger.Warning(
                    "[LocationNpc] BeginMission({New}) while {Old} was still active, replacing it",
                    instanceId,
                    activeInstanceId);
            }

            activeInstanceId = instanceId;
            localHostConfirmed = false;
            this.partyAgentResolver = partyAgentResolver;
        }

        SuppressCapture = false;
        IsReplayingNativePopulation = false;
    }

    public void EndMission()
    {
        lock (stateLock)
        {
            activeInstanceId = null;
            localHostConfirmed = false;
            partyAgentResolver = null;
        }

        SuppressCapture = false;
        IsReplayingNativePopulation = false;
    }

    public void SetLocalHost(string instanceId, bool isLocalHost)
    {
        lock (stateLock)
        {
            if (activeInstanceId == null || activeInstanceId != instanceId) return;
            localHostConfirmed = isLocalHost;
        }
    }

    public void Dispose()
    {
        suppressCapture.Dispose();
        isReplayingNativePopulation.Dispose();
    }
}
