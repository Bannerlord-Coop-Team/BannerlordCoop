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

    /// <summary>True on this thread inside a <see cref="SuppressCapture"/> scope.</summary>
    bool IsCaptureSuppressed { get; }

    /// <summary>True on this thread inside a <see cref="ReplayNativePopulation"/> scope.</summary>
    bool IsReplayingNativePopulation { get; }

    /// <summary>Keeps spawns on this thread from being captured for NPC replication until the scope is disposed.</summary>
    IDisposable SuppressCapture();

    /// <summary>Marks this thread as replaying the native population pass until the scope is disposed.</summary>
    IDisposable ReplayNativePopulation();

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
    public bool IsCaptureSuppressed => suppressCapture.Value;

    /// <inheritdoc/>
    public bool IsReplayingNativePopulation => isReplayingNativePopulation.Value;

    /// <inheritdoc/>
    public IDisposable SuppressCapture() => new ThreadFlagScope(suppressCapture);

    /// <inheritdoc/>
    public IDisposable ReplayNativePopulation() => new ThreadFlagScope(isReplayingNativePopulation);

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
    }

    /// <inheritdoc/>
    public void EndMission()
    {
        lock (stateLock)
        {
            activeInstanceId = null;
            partyAgentResolver = null;
        }
    }

    /// <summary>Releases the per-thread flag storage.</summary>
    public void Dispose()
    {
        suppressCapture.Dispose();
        isReplayingNativePopulation.Dispose();
    }

    /// <summary>
    /// Sets a per-thread flag for its lifetime and restores the previous value on dispose, so nested scopes unwind correctly.
    /// </summary>
    private sealed class ThreadFlagScope : IDisposable
    {
        private readonly bool previousValue;
        private readonly ThreadLocal<bool> flag;

        /// <summary>Raises the flag on the current thread, remembering its previous value.</summary>
        public ThreadFlagScope(ThreadLocal<bool> flag)
        {
            this.flag = flag;
            previousValue = flag.Value;
            flag.Value = true;
        }

        /// <summary>Restores the flag's previous value.</summary>
        public void Dispose()
        {
            flag.Value = previousValue;
        }
    }
}
