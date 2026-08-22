using Common.Messaging;

namespace Common.Network.Session.Messages;

/// <summary>Requests joining a session advertised by one storefront provider.</summary>
public record JoinSessionListing : ICommand
{
    public SessionListingId ListingId { get; }

    public JoinSessionListing(SessionListingId listingId)
    {
        ListingId = listingId;
    }
}
