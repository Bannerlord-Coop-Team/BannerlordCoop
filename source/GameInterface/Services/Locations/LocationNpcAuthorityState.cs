using Common.Logging;
using GameInterface.Services.Locations.Hosting;
using Serilog;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations;

/// <summary>
/// Per-client state for the active settlement location mission and its confirmed NPC authority.
/// Missions writes it; the location Harmony patches resolve it from the active client's container.
/// </summary>
public interface ILocationNpcAuthorityState
{
    /// <summary>True while a coop settlement location mission is active on this client.</summary>
    bool IsCoopLocationMissionActive { get; }

    /// <summary>The active location instance id, or null when no location mission is active.</summary>
    string ActiveInstanceId { get; }

    /// <summary>True while the server's host assignment for the active instance names this client.</summary>
    bool IsLocalHostConfirmed { get; }

    /// <summary>True while native population spawns must be skipped (active mission, host not confirmed).</summary>
    bool ShouldSuppressNativeSpawns { get; }

    /// <summary>True on this thread while a spawn must not be captured for NPC replication.</summary>
    bool SuppressCapture { get; set; }

    /// <summary>True on this thread while the elected host replays the native population pass.</summary>
    bool IsReplayingNativePopulation { get; set; }

    /// <summary>True if the agent is the main agent, a local party member or a known remote party puppet.</summary>
    bool IsPlayerPartyAgent(Agent agent, Agent mainAgent);

    /// <summary>Starts tracking a location mission; native spawns stay suppressed until host confirmation.</summary>
    void BeginMission(string instanceId, Func<Agent, bool> partyAgentResolver = null);

    /// <summary>Stops tracking the active location mission.</summary>
    void EndMission();
}

/// <summary>
/// Tracks the active settlement location mission; NPC host confirmation is read from <see cref="ILocationHostRegistry"/>.
/// </summary>
public class LocationNpcAuthorityState : ILocationNpcAuthorityState, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationNpcAuthorityState>();

    private readonly ILocationHostRegistry hostRegistry;
    private readonly object stateLock = new object();
    private readonly ThreadLocal<bool> suppressCapture = new ThreadLocal<bool>();
    private readonly ThreadLocal<bool> isReplayingNativePopulation = new ThreadLocal<bool>();
    private string activeInstanceId;
    private Func<Agent, bool> partyAgentResolver;

    /// <summary>Reads NPC host confirmation from this client's location host assignments.</summary>
    public LocationNpcAuthorityState(ILocationHostRegistry hostRegistry)
    {
        this.hostRegistry = hostRegistry;
    }

    /// <inheritdoc/>
    public bool IsCoopLocationMissionActive
    {
        get { lock (stateLock) return activeInstanceId != null; }
    }

    /// <inheritdoc/>
    public string ActiveInstanceId
    {
        get { lock (stateLock) return activeInstanceId; }
    }

    /// <inheritdoc/>
    public bool IsLocalHostConfirmed
    {
        get
        {
            string instanceId = ActiveInstanceId;
            return instanceId != null && hostRegistry.IsHost(instanceId);
        }
    }

    /// <inheritdoc/>
    public bool ShouldSuppressNativeSpawns
    {
        get
        {
            string instanceId = ActiveInstanceId;
            return instanceId != null && !hostRegistry.IsHost(instanceId);
        }
    }

    /// <inheritdoc/>
    public bool SuppressCapture
    {
        get => suppressCapture.Value;
        set => suppressCapture.Value = value;
    }

    /// <inheritdoc/>
    public bool IsReplayingNativePopulation
    {
        get => isReplayingNativePopulation.Value;
        set => isReplayingNativePopulation.Value = value;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
            this.partyAgentResolver = partyAgentResolver;
        }

        SuppressCapture = false;
        IsReplayingNativePopulation = false;
    }

    /// <inheritdoc/>
    public void EndMission()
    {
        lock (stateLock)
        {
            activeInstanceId = null;
            partyAgentResolver = null;
        }

        SuppressCapture = false;
        IsReplayingNativePopulation = false;
    }

    /// <summary>Releases the per-thread flag storage.</summary>
    public void Dispose()
    {
        suppressCapture.Dispose();
        isReplayingNativePopulation.Dispose();
    }
}
