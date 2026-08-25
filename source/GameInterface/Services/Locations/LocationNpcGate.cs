using Common.Logging;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations;

/// <summary>
/// Process-global bridge between the location NPC host stack (Missions assembly) and the static Harmony
/// patches in this assembly, for the CURRENT settlement location mission. Deliberately separate from
/// <see cref="MapEvents.BattleSpawnGate"/> — that flag drives the battle patch families, and a location
/// mission must never engage them.
/// <para>
/// It holds no election logic: <c>CoopLocationsController</c> calls <see cref="BeginMission"/> /
/// <see cref="EndMission"/> around the mission's lifetime, and the location host handler confirms (or
/// revokes) local host authority via <see cref="SetLocalHost"/> when the server's assignment arrives.
/// Until the local client is a CONFIRMED host, native population spawning is suppressed
/// (<see cref="ShouldSuppressNativeSpawns"/>) — the host is unknown while the mission loads (SR-013).
/// </para>
/// </summary>
public static class LocationNpcGate
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(LocationNpcGate));

    private static readonly object Gate = new object();

    private static string _activeInstanceId;
    private static bool _localHostConfirmed;
    private static Func<Agent, bool> _partyAgentResolver;

    // Set around a puppet Mission.SpawnAgent/SpawnMonster call so the location capture patch does not
    // re-capture (and re-broadcast) an agent that itself came off the wire. Thread-static because it is
    // set and read on the same thread within a single spawn call.
    [ThreadStatic]
    private static bool _suppressCapture;

    [ThreadStatic]
    private static bool _isReplayingNativePopulation;

    /// <summary>True while a coop settlement location mission is active on this client.</summary>
    public static bool IsCoopLocationMissionActive
    {
        get { lock (Gate) return _activeInstanceId != null; }
    }

    /// <summary>The active location instance id ("{settlementId}|{locationId}"), or null.</summary>
    public static string ActiveInstanceId
    {
        get { lock (Gate) return _activeInstanceId; }
    }

    /// <summary>True once the server's assignment named THIS client the NPC host of the active mission.</summary>
    public static bool IsLocalHostConfirmed
    {
        get { lock (Gate) return _localHostConfirmed; }
    }

    /// <summary>
    /// True when the native population spawn paths must be skipped on this client: a coop location
    /// mission is active and local host authority has not been confirmed (SR-012/SR-013). The flag
    /// flips off the moment the local client is confirmed host — native systems then run and the
    /// capture patch replicates what they spawn.
    /// </summary>
    public static bool ShouldSuppressNativeSpawns
    {
        get { lock (Gate) return _activeInstanceId != null && !_localHostConfirmed; }
    }

    /// <inheritdoc cref="_suppressCapture"/>
    public static bool SuppressCapture
    {
        get => _suppressCapture;
        set => _suppressCapture = value;
    }

    /// <summary>True while the elected host replays the native population pass after party agents spawned.</summary>
    public static bool IsReplayingNativePopulation
    {
        get => _isReplayingNativePopulation;
        set => _isReplayingNativePopulation = value;
    }

    /// <summary>True when the agent belongs to a local or replicated player party.</summary>
    public static bool IsPlayerPartyAgent(Agent agent)
        => IsPlayerPartyAgent(agent, Agent.Main);

    internal static bool IsPlayerPartyAgent(Agent agent, Agent mainAgent)
    {
        if (agent == null) return false;
        if (ReferenceEquals(agent, mainAgent)) return true;

        if (agent.Origin is PartyAgentOrigin origin)
        {
            var mainParty = PartyBase.MainParty;
            if (mainParty != null && origin.Party == mainParty)
                return true;
        }

        Func<Agent, bool> resolver;
        lock (Gate) resolver = _partyAgentResolver;
        return resolver?.Invoke(agent) == true;
    }

    /// <summary>A coop location mission began on this client. Resets host confirmation.</summary>
    public static void BeginMission(string instanceId, Func<Agent, bool> partyAgentResolver = null)
    {
        if (string.IsNullOrEmpty(instanceId)) throw new ArgumentException("instanceId is required", nameof(instanceId));

        lock (Gate)
        {
            if (_activeInstanceId != null && _activeInstanceId != instanceId)
                Logger.Warning("[LocationNpc] BeginMission({New}) while {Old} was still active — replacing (missed EndMission?)",
                    instanceId, _activeInstanceId);

            _activeInstanceId = instanceId;
            _localHostConfirmed = false;
            _partyAgentResolver = partyAgentResolver;
        }
    }

    /// <summary>The location mission ended on this client.</summary>
    public static void EndMission()
    {
        lock (Gate)
        {
            _activeInstanceId = null;
            _localHostConfirmed = false;
            _partyAgentResolver = null;
        }
    }

    /// <summary>
    /// Record whether THIS client is the confirmed NPC host — but only when the assignment is for the
    /// ACTIVE mission instance; a stale assignment from a previous settlement visit must not flip the
    /// gate of a newer mission.
    /// </summary>
    public static void SetLocalHost(string instanceId, bool isLocalHost)
    {
        lock (Gate)
        {
            if (_activeInstanceId == null || _activeInstanceId != instanceId)
                return;

            _localHostConfirmed = isLocalHost;
        }
    }
}
