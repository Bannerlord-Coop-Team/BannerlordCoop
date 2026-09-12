using GameInterface.Services.Entity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Tournaments;

/// <summary>
/// Mission-local projection of the campaign server's tournament snapshot. It deliberately stores stable ids and
/// revisions only; native tournament list indices are never used as network identity.
/// </summary>
public interface ITournamentMissionSession
{
    string SessionId { get; }
    string InstanceId { get; }
    string CurrentMatchId { get; }
    string OwnControllerId { get; }
    string HostControllerId { get; }
    long Revision { get; }
    long BracketRevision { get; }
    bool HasInstance { get; }
    bool IsLocalHost { get; }
    IReadOnlyCollection<string> SuccessorControllerIds { get; }

    bool TryApplyState(
        string sessionId,
        string instanceId,
        long revision,
        long bracketRevision,
        string currentMatchId,
        string hostControllerId,
        IEnumerable<string> successorControllerIds);
    bool IsOwn(string controllerId);
    bool IsHostController(string controllerId);
    void Reset();
}

/// <inheritdoc cref='ITournamentMissionSession'/>
public class TournamentMissionSession : ITournamentMissionSession
{
    private readonly object gate = new();
    private readonly IControllerIdProvider controllerIdProvider;
    private string[] successorControllerIds = Array.Empty<string>();
    private bool initialized;

    public TournamentMissionSession(IControllerIdProvider controllerIdProvider)
    {
        this.controllerIdProvider = controllerIdProvider;
    }

    public string SessionId { get; private set; }
    public string InstanceId { get; private set; }
    public string CurrentMatchId { get; private set; }
    public string HostControllerId { get; private set; }
    public long Revision { get; private set; }
    public long BracketRevision { get; private set; }
    public string OwnControllerId => controllerIdProvider.ControllerId;
    public bool HasInstance => !string.IsNullOrEmpty(InstanceId);
    public bool IsLocalHost => IsHostController(OwnControllerId);

    public IReadOnlyCollection<string> SuccessorControllerIds
    {
        get
        {
            lock (gate)
            {
                return successorControllerIds.ToArray();
            }
        }
    }

    public bool TryApplyState(
        string sessionId,
        string instanceId,
        long revision,
        long bracketRevision,
        string currentMatchId,
        string hostControllerId,
        IEnumerable<string> successors)
    {
        if (string.IsNullOrEmpty(sessionId)) return false;
        if (string.IsNullOrEmpty(instanceId)) return false;
        if (revision < 0 || bracketRevision < 0) return false;

        lock (gate)
        {
            if (initialized && SessionId != sessionId) return false;
            if (initialized && revision <= Revision) return false;

            initialized = true;
            SessionId = sessionId;
            InstanceId = instanceId;
            Revision = revision;
            BracketRevision = bracketRevision;
            CurrentMatchId = currentMatchId;
            HostControllerId = hostControllerId;
            successorControllerIds = successors?
                .Where(controllerId => !string.IsNullOrEmpty(controllerId))
                .Distinct()
                .ToArray() ?? Array.Empty<string>();
            return true;
        }
    }

    public bool IsOwn(string controllerId) => controllerId == OwnControllerId;

    public bool IsHostController(string controllerId)
    {
        lock (gate)
        {
            return initialized
                   && !string.IsNullOrEmpty(controllerId)
                   && HostControllerId == controllerId;
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            initialized = false;
            SessionId = null;
            InstanceId = null;
            CurrentMatchId = null;
            HostControllerId = null;
            Revision = 0;
            BracketRevision = 0;
            successorControllerIds = Array.Empty<string>();
        }
    }
}
