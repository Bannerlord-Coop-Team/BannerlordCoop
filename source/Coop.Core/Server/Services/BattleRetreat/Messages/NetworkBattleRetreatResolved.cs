using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.BattleRetreat.Messages;

/// <summary>
/// Server's verdict on a retreat request, broadcast so every client converges on the same outcome.
/// </summary>
/// <remarks>
/// Broadcast rather than unicast, and self-filtering by id: the requester runs its local menu
/// continuation, parties whose siege camp the retreat dissolved clean up their own siege state, and
/// every other client applies nothing. The roster and item losses are NOT in here - they arrive as
/// ordinary replicated deltas from the server's own mutations.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public record NetworkBattleRetreatResolved : IEvent
{
    [ProtoMember(1)]
    public string PartyId { get; }

    [ProtoMember(2)]
    public bool Approved { get; }

    /// <summary>Parties whose besieger camp the retreat cleared, so they can drop their local siege state.</summary>
    [ProtoMember(3)]
    public string[] CampClearedPartyIds { get; }

    public NetworkBattleRetreatResolved(string partyId, bool approved, string[] campClearedPartyIds)
    {
        PartyId = partyId;
        Approved = approved;
        CampClearedPartyIds = campClearedPartyIds;
    }
}
