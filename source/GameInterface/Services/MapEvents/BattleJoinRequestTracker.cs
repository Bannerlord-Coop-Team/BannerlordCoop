using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Tracks this client's battle join requests that wait for the server's reply, one per party.
/// </summary>
public interface IBattleJoinRequestTracker : IGameAbstraction
{
    /// <summary>Records a join request to the map event; false while the party already waits for a reply.</summary>
    bool TryStart(PartyBase party, MapEvent mapEvent, string requestId);

    /// <summary>Marks the party's waiting join to the map event cancelled, so an accepted reply takes the party out again; false when no join to it waits.</summary>
    bool TryCancel(PartyBase party, MapEvent mapEvent);

    /// <summary>Whether the party's waiting join was cancelled.</summary>
    bool IsCancelled(PartyBase party);

    /// <summary>Ends the request a reply answers and says whether it was cancelled; false for an unknown or older request.</summary>
    bool TryComplete(PartyBase party, string requestId, out bool cancelled);

    /// <summary>Drops every waiting request.</summary>
    void Clear();
}

/// <summary>
/// Holds each waiting request by party, with its map event and whether the player cancelled it.
/// </summary>
internal sealed class BattleJoinRequestTracker : IBattleJoinRequestTracker
{
    private readonly ConcurrentDictionary<PartyBase, (string RequestId, MapEvent MapEvent, bool Cancelled)> pendingRequests =
        new ConcurrentDictionary<PartyBase, (string RequestId, MapEvent MapEvent, bool Cancelled)>();

    public bool TryStart(PartyBase party, MapEvent mapEvent, string requestId) => pendingRequests.TryAdd(party, (requestId, mapEvent, false));

    public bool TryCancel(PartyBase party, MapEvent mapEvent)
    {
        if (!pendingRequests.TryGetValue(party, out var request)) return false;
        if (request.MapEvent != mapEvent) return false;

        return pendingRequests.TryUpdate(party, (request.RequestId, mapEvent, true), request);
    }

    public bool IsCancelled(PartyBase party) => pendingRequests.TryGetValue(party, out var request) && request.Cancelled;

    public bool TryComplete(PartyBase party, string requestId, out bool cancelled)
    {
        cancelled = false;
        if (!pendingRequests.TryGetValue(party, out var request)) return false;
        if (!string.Equals(request.RequestId, requestId, StringComparison.Ordinal)) return false;

        cancelled = request.Cancelled;
        return pendingRequests.TryRemove(party, out _);
    }

    public void Clear() => pendingRequests.Clear();
}
