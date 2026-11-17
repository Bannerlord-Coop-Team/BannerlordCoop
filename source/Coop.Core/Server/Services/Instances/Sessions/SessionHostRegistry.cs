using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Instances.Sessions;

/// <summary>The host of a campaign-free session, the successor line and the hosting generation.</summary>
public sealed class SessionHostAssignment
{
    public SessionHostAssignment(string hostControllerId, IReadOnlyList<string> successorControllerIds, int epoch)
    {
        HostControllerId = hostControllerId;
        SuccessorControllerIds = successorControllerIds;
        Epoch = epoch;
    }

    public string HostControllerId { get; }
    public IReadOnlyList<string> SuccessorControllerIds { get; }
    public int Epoch { get; }
}

/// <summary>
/// [Server] The host assignment of each open session. Separate from the battle and location registries:
/// the battle host handler reads the map event, which a session does not have.
/// </summary>
public interface ISessionHostRegistry
{
    bool TryGet(string sessionId, out SessionHostAssignment assignment);

    void Set(string sessionId, SessionHostAssignment assignment);

    /// <summary>Drops the assignment and keeps the epoch watermark, so a re-election issues a higher epoch.</summary>
    bool Remove(string sessionId);

    /// <summary>Returns max(last issued, <paramref name="floor"/>) + 1 and records it as the last issued epoch.</summary>
    int NextEpoch(string sessionId, int floor = 0);
}

/// <inheritdoc cref="ISessionHostRegistry"/>
public class SessionHostRegistry : ISessionHostRegistry
{
    private readonly Dictionary<string, SessionHostAssignment> assignments = new Dictionary<string, SessionHostAssignment>();
    private readonly Dictionary<string, int> issuedEpochs = new Dictionary<string, int>();
    private readonly object gate = new object();

    public bool TryGet(string sessionId, out SessionHostAssignment assignment)
    {
        lock (gate)
        {
            return assignments.TryGetValue(sessionId, out assignment);
        }
    }

    public void Set(string sessionId, SessionHostAssignment assignment)
    {
        lock (gate)
        {
            assignments[sessionId] = assignment;
        }
    }

    public bool Remove(string sessionId)
    {
        lock (gate)
        {
            return assignments.Remove(sessionId);
        }
    }

    public int NextEpoch(string sessionId, int floor = 0)
    {
        lock (gate)
        {
            issuedEpochs.TryGetValue(sessionId, out var last);
            var next = Math.Max(last, floor) + 1;
            issuedEpochs[sessionId] = next;
            return next;
        }
    }
}
