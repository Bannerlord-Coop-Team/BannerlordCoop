using Common;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Client-side reusable plumbing for a "send a correlated request, block the game thread until the server's reply
/// (or a deadline) arrives" round trip. Correlates each reply to its request by an opaque request id and pumps the
/// game thread while waiting (<see cref="GameThread.WaitWhilePumping"/>), so the network thread — which delivers the
/// reply — keeps making progress instead of deadlocking against the blocked game-loop thread.
/// </summary>
/// <remarks>
/// Extracted from the duplicated round-trip plumbing that <see cref="BattleStartCoordinator"/> and
/// <see cref="MapEventCreationCoordinator"/> each used to hand-roll. This owns only the mechanical
/// correlate-and-wait; every caller keeps its own request/reply message types, its own deadline policy, and any
/// post-reply phases (e.g. the map-event coordinator's "both sides attached" wait).
/// <typeparamref name="TReply"/> is whatever the reply carries — an accepted flag, a server-assigned id, etc.
/// </remarks>
internal sealed class BlockingRequestGate<TReply>
{
    private readonly ConcurrentDictionary<string, PendingRequest> pendingRequests =
        new ConcurrentDictionary<string, PendingRequest>();

    /// <summary>
    /// Registers a new in-flight request and returns its handle. Carry <see cref="PendingRequest.RequestId"/> as the
    /// correlation id in the request message, then pass the handle to <see cref="WaitWhilePumping"/>.
    /// </summary>
    public PendingRequest Register()
    {
        var pending = new PendingRequest(Guid.NewGuid().ToString());
        pendingRequests[pending.RequestId] = pending;
        return pending;
    }

    /// <summary>
    /// [Game thread] Blocks (pumping the game loop) until the request completes or <paramref name="deadline"/>
    /// passes. Returns true when the reply arrived — its value is then in <see cref="PendingRequest.Reply"/> — and
    /// false on timeout.
    /// </summary>
    public bool WaitWhilePumping(PendingRequest pending, DateTime deadline)
        => GameThread.WaitWhilePumping(() => pending.Completed.IsSet, deadline);

    /// <summary>
    /// Removes the request's tracking entry. Call in a <c>finally</c> after the wait, whether it completed or
    /// timed out.
    /// </summary>
    public void Release(PendingRequest pending)
        => pendingRequests.TryRemove(pending.RequestId, out _);

    /// <summary>
    /// [Reply handler] Completes the matching in-flight request with <paramref name="reply"/>. Returns false when no
    /// request matches the id (already released after a timeout, or a reply meant for another instance), so the
    /// caller can decide whether that is worth logging.
    /// </summary>
    public bool Complete(string requestId, TReply reply)
    {
        if (pendingRequests.TryGetValue(requestId, out var pending))
        {
            pending.Reply = reply;
            pending.Completed.Set();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tracks a single in-flight request. <see cref="Completed"/> is deliberately not disposed: the network thread
    /// may signal it concurrently with the requesting thread giving up, and a low-frequency battle event does not
    /// justify the extra synchronization to dispose it safely.
    /// </summary>
    internal sealed class PendingRequest
    {
        public PendingRequest(string requestId)
        {
            RequestId = requestId;
        }

        public string RequestId { get; }
        public ManualResetEventSlim Completed { get; } = new ManualResetEventSlim(false);
        public TReply Reply { get; set; }
    }
}
