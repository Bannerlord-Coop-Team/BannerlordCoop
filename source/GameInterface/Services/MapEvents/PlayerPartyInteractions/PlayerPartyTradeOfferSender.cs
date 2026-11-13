using Common.Logging;
using Common.Network;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Coalesces the local trade offer into one send per flush, because vanilla recomputes it after every item change.
public interface IPlayerPartyTradeOfferSender
{
    // Forgets the last sent offer and any pending change, because a new trade opens with nothing offered.
    void Begin();

    // Records that the offer on this barter screen may have changed since the last flush.
    void MarkChanged(BarterVM barterVM);

    // Sends the pending offer when it differs from the last one sent, logging any failure.
    void Flush();
}

// Scoped per client, because the barter patches and the application tick must share one pending offer.
internal class PlayerPartyTradeOfferSender : IPlayerPartyTradeOfferSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyTradeOfferSender>();
    private static readonly NetworkPlayerPartyTradeOfferUpdated EmptyOffer = new NetworkPlayerPartyTradeOfferUpdated(null, null, null, null);

    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerPartyTradeOfferBuilder offerBuilder;

    private BarterVM changedBarterVM;
    private NetworkPlayerPartyTradeOfferUpdated lastSentOffer = EmptyOffer;

    public PlayerPartyTradeOfferSender(INetwork network, IObjectManager objectManager, IPlayerPartyTradeOfferBuilder offerBuilder)
    {
        this.network = network;
        this.objectManager = objectManager;
        this.offerBuilder = offerBuilder;
    }

    // Resets the baseline the next offer is compared against.
    public void Begin()
    {
        changedBarterVM = null;
        lastSentOffer = EmptyOffer;
    }

    // Keeps the barter screen to build from at the next flush.
    public void MarkChanged(BarterVM barterVM)
        => changedBarterVM = barterVM;

    // Clears the pending change before sending, so a failing offer logs once instead of every frame.
    public void Flush()
    {
        var barterVM = changedBarterVM;
        if (barterVM == null) return;
        changedBarterVM = null;

        try
        {
            SendIfChanged(barterVM);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send the player-party trade offer");
        }
    }

    // An NPC barter is settled locally, and a sent offer clears both acceptances the same as the server does when it arrives.
    private void SendIfChanged(BarterVM barterVM)
    {
        if (!PlayerPartyTradeContext.IsActive) return;
        if (!objectManager.TryGetIdWithLogging(PlayerPartyTradeContext.LocalParty, out var partyId)) return;

        var offer = offerBuilder.Build(PlayerPartyTradeContext.SessionId, partyId, barterVM);
        if (IsSameOffer(offer, lastSentOffer)) return;

        network.SendAll(offer);
        lastSentOffer = offer;
        PlayerPartyTradeContext.ResetAcceptance();
    }

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
