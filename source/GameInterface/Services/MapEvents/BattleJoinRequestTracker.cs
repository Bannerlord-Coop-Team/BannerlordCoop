using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Tracks this client's battle join requests that wait for the server's reply, one per party.
/// </summary>
public interface IBattleJoinRequestTracker : IGameAbstraction
{
    /// <summary>Records a join request; false while the party already waits for a reply.</summary>
    bool TryStart(string partyId, string requestId);

    /// <summary>Marks the party's waiting join cancelled, so an accepted reply takes the party out again; false when none waits.</summary>
    bool TryCancel(PartyBase party);

    /// <summary>Ends the request a reply answers and says whether it was cancelled; false for an unknown or older request.</summary>
    bool TryComplete(string partyId, string requestId, out bool cancelled);

    /// <summary>Drops every waiting request.</summary>
    void Clear();
}

/// <summary>
/// Holds each waiting request id by party id, with whether the player cancelled it.
/// </summary>
internal sealed class BattleJoinRequestTracker : IBattleJoinRequestTracker
{
    private readonly IObjectManager objectManager;
    private readonly ConcurrentDictionary<string, (string RequestId, bool Cancelled)> pendingRequests =
        new ConcurrentDictionary<string, (string RequestId, bool Cancelled)>();

    public BattleJoinRequestTracker(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryStart(string partyId, string requestId) => pendingRequests.TryAdd(partyId, (requestId, false));

    public bool TryCancel(PartyBase party)
    {
        if (!objectManager.TryGetId(party, out var partyId)) return false;
        if (!pendingRequests.TryGetValue(partyId, out var request)) return false;

        return pendingRequests.TryUpdate(partyId, (request.RequestId, true), request);
    }

    public bool TryComplete(string partyId, string requestId, out bool cancelled)
    {
        cancelled = false;
        if (!pendingRequests.TryGetValue(partyId, out var request)) return false;
        if (!string.Equals(request.RequestId, requestId, StringComparison.Ordinal)) return false;

        cancelled = request.Cancelled;
        return pendingRequests.TryRemove(partyId, out _);
    }

    public void Clear() => pendingRequests.Clear();
}
