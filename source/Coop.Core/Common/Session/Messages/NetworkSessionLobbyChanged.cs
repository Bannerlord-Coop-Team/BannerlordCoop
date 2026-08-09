using Common.Messaging;
using Common.Network.Session;
using ProtoBuf;

namespace Coop.Core.Common.Session.Messages;

/// <summary>
/// Identifies the provider listing owned by the authoritative server.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSessionLobbyChanged : IEvent
{
    [ProtoMember(1)]
    public readonly string Provider;

    [ProtoMember(2)]
    public readonly string ListingId;

    public NetworkSessionLobbyChanged(SessionListingId listingId)
    {
        Provider = listingId.Provider;
        ListingId = listingId.Value;
    }

    public SessionListingId ToListingId() => new SessionListingId(Provider, ListingId);
}
