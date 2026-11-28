using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Tracks this client's battle join requests that wait for the server's reply, one per party.
/// </summary>
public interface IBattleJoinRequestTracker : IGameAbstraction
{
    /// <summary>Records a join request to the map event; false while the party already has a waiting or cancelled join.</summary>
    bool TryStart(PartyBase party, MapEvent mapEvent, string requestId);

    /// <summary>Marks the party's join to the map event cancelled, so an accepted reply takes the party out again; false when the party has no join to it.</summary>
    bool TryCancel(PartyBase party, MapEvent mapEvent);

    /// <summary>Whether the party's join to the map event was cancelled and not yet ended by <see cref="EndCancelled"/>.</summary>
    bool IsCancelled(PartyBase party, MapEvent mapEvent);

    /// <summary>Ends the request a reply answers and says whether it was cancelled, which keeps that cancel until <see cref="EndCancelled"/>; false for an unknown or older request.</summary>
    bool TryComplete(PartyBase party, string requestId, out bool cancelled);

    /// <summary>Drops the party's answered cancelled join, once the server removed the party or never added it.</summary>
    void EndCancelled(PartyBase party);

    /// <summary>Drops every request.</summary>
    void Clear();
}

/// <summary>
/// Holds each request by party, with its map event and how far the player's cancel of it got.
/// </summary>
internal sealed class BattleJoinRequestTracker : IBattleJoinRequestTracker
{
    private enum JoinState { Waiting, Cancelled, CancelAnswered }

    private readonly ConcurrentDictionary<PartyBase, (string RequestId, MapEvent MapEvent, JoinState State)> requests =
        new ConcurrentDictionary<PartyBase, (string RequestId, MapEvent MapEvent, JoinState State)>();

    public bool TryStart(PartyBase party, MapEvent mapEvent, string requestId) => requests.TryAdd(party, (requestId, mapEvent, JoinState.Waiting));

    public bool TryCancel(PartyBase party, MapEvent mapEvent)
    {
        if (!requests.TryGetValue(party, out var request)) return false;
        if (request.MapEvent != mapEvent) return false;

        return requests.TryUpdate(party, (request.RequestId, mapEvent, JoinState.Cancelled), request);
    }

    public bool IsCancelled(PartyBase party, MapEvent mapEvent) =>
        requests.TryGetValue(party, out var request) && request.MapEvent == mapEvent && request.State != JoinState.Waiting;

    public bool TryComplete(PartyBase party, string requestId, out bool cancelled)
    {
        cancelled = false;
        if (!requests.TryGetValue(party, out var request)) return false;
        if (!string.Equals(request.RequestId, requestId, StringComparison.Ordinal)) return false;

        cancelled = request.State == JoinState.Cancelled;
        if (cancelled)
            return requests.TryUpdate(party, (request.RequestId, request.MapEvent, JoinState.CancelAnswered), request);

        return requests.TryRemove(party, out _);
    }

    public void EndCancelled(PartyBase party)
    {
        if (!requests.TryGetValue(party, out var request)) return;
        if (request.State != JoinState.CancelAnswered) return;

        // Removes the entry only while it is still the one read above.
        ((ICollection<KeyValuePair<PartyBase, (string, MapEvent, JoinState)>>)requests).Remove(
            new KeyValuePair<PartyBase, (string, MapEvent, JoinState)>(party, request));
    }

    public void Clear() => requests.Clear();
}
