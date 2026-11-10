using Common.Network;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using System.Linq;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Coalesces the local trade offer into one send per flush, because vanilla recomputes it after every item change.
public interface IPlayerPartyTradeOfferSender
{
    // Records that the local offer may have changed since the last flush.
    void MarkChanged();

    // Sends the local offer when it was marked changed and differs from the last one sent.
    void Flush();
}

// Keeps the pending flag and last sent offer per client, because E2E clients share one process.
internal class PlayerPartyTradeOfferSender : IPlayerPartyTradeOfferSender
{
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerPartyTradeOfferBuilder offerBuilder;

    private bool isChanged;
    private NetworkPlayerPartyTradeOfferUpdated lastSentOffer;

    public PlayerPartyTradeOfferSender(INetwork network, IObjectManager objectManager, IPlayerPartyTradeOfferBuilder offerBuilder)
    {
        this.network = network;
        this.objectManager = objectManager;
        this.offerBuilder = offerBuilder;
    }

    // Only a player-party trade sends offers, an NPC barter is settled locally.
    public void MarkChanged()
    {
        if (!PlayerPartyTradeContext.IsActive) return;

        isChanged = true;
    }

    // Changing the offer clears both acceptances, the same as the server does when the offer arrives.
    public void Flush()
    {
        if (!isChanged) return;
        isChanged = false;

        var barterVM = PlayerPartyTradeContext.ActiveBarterVM;
        if (barterVM == null) return;
        if (!objectManager.TryGetIdWithLogging(PlayerPartyTradeContext.LocalParty, out var partyId)) return;

        var offer = offerBuilder.Build(PlayerPartyTradeContext.SessionId, partyId, barterVM);
        if (IsSameOffer(offer, GetLastSentOffer(offer))) return;

        lastSentOffer = offer;
        PlayerPartyTradeContext.ResetAcceptance();
        network.SendAll(offer);
    }

    // A trade opens with nothing offered, so a new session compares against an empty offer.
    private NetworkPlayerPartyTradeOfferUpdated GetLastSentOffer(NetworkPlayerPartyTradeOfferUpdated offer)
        => lastSentOffer.SessionId == offer.SessionId
            ? lastSentOffer
            : new NetworkPlayerPartyTradeOfferUpdated(offer.SessionId, offer.PartyId, null, null);

    // Compares every offered field, so only a real change reaches the network.
    private static bool IsSameOffer(NetworkPlayerPartyTradeOfferUpdated offer, NetworkPlayerPartyTradeOfferUpdated other)
        => offer.OfferedGold == other.OfferedGold &&
           offer.OfferedPeace == other.OfferedPeace &&
           offer.OfferedItems.SequenceEqual(other.OfferedItems) &&
           offer.OfferedTroops.SequenceEqual(other.OfferedTroops) &&
           offer.OfferedFiefs.SequenceEqual(other.OfferedFiefs) &&
           offer.OfferedPrisoners.SequenceEqual(other.OfferedPrisoners) &&
           offer.OfferedMarriages.SequenceEqual(other.OfferedMarriages);
}
