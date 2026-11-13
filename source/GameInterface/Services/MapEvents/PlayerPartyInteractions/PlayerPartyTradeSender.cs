using Common;
using Common.Logging;
using Common.Network;
using Common.Serialization;
using GameInterface.Services.MapEvents.Messages.Conversation;
using Serilog;
using System;
using System.Linq;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Sends this client's trade offer, accept and leave in the order they happened, coalescing offers because vanilla recomputes one after every item change.
internal interface IPlayerPartyTradeSender
{
    // Records that the local offer may have changed and schedules one flush for the burst.
    void MarkChanged();

    // Sends the pending offer when it differs from the last one sent for this trade, retrying one whose send failed.
    void Flush();

    // Sends the pending offer and then the local accept; false when no player trade is open, so vanilla settles the barter.
    bool Accept();

    // Sends the pending offer and then leaves the trade; false when no player trade is open, so vanilla settles the barter.
    bool Leave();
}

// Keeps only the sending state, the trade session itself belongs to the trade context.
internal class PlayerPartyTradeSender : IPlayerPartyTradeSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyTradeSender>();

    private readonly INetwork network;
    private readonly ICommonSerializer serializer;
    private readonly IPlayerPartyTradeOfferBuilder offerBuilder;
    private readonly IPlayerPartyTradeContext trade;

    private bool changePending;
    private string failedSendSessionId;
    private (string SessionId, byte[] Offer) lastSent;

    public PlayerPartyTradeSender(
        INetwork network,
        ICommonSerializer serializer,
        IPlayerPartyTradeOfferBuilder offerBuilder,
        IPlayerPartyTradeContext trade)
    {
        this.network = network;
        this.serializer = serializer;
        this.offerBuilder = offerBuilder;
        this.trade = trade;
    }

    // True while this trade's last offer send failed, so the server may still hold an older offer than the screen.
    private bool HasUnsentOffer => trade.IsActive && failedSendSessionId == trade.SessionId;

    // An NPC barter is settled locally, so it never schedules a send.
    public void MarkChanged()
    {
        if (!trade.IsActive) return;
        if (changePending) return;

        changePending = true;
        GameThread.EnqueueSafe(Flush, context: "Send player-party trade offer");
    }

    public void Flush()
    {
        var wasChangePending = changePending;
        // Cleared before the trade check, so a stale flush cant carry into the next trade.
        changePending = false;
        if (!trade.IsActive) return;
        if (!wasChangePending && !HasUnsentOffer) return;

        bool sent;
        try
        {
            sent = SendIfChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send the player-party trade offer");
            failedSendSessionId = trade.SessionId;
            return;
        }

        failedSendSessionId = null;
        // Clears both acceptances like the server does, outside the try so a refresh failure isnt taken for a failed send.
        if (sent)
            trade.ResetAcceptance();
    }

    // A pending edit goes out first, because it clears the acceptance this accept is checked against and must reach the server ahead of it.
    public bool Accept()
    {
        if (!trade.IsActive) return false;

        Flush();
        // An offer that failed to send isnt accepted, otherwise the server would apply the older offer it still holds.
        if (HasUnsentOffer) return true;
        if (trade.LocalAccepted) return true;

        trade.SetLocalAccepted();
        network.SendAll(new NetworkPlayerPartyTradeAcceptChanged(trade.SessionId, accepted: true));
        return true;
    }

    // A pending edit goes out first, because it clears the acceptance that would block leaving.
    public bool Leave()
    {
        if (!trade.IsActive) return false;

        Flush();
        if (trade.LocalAccepted) return true;

        network.SendAll(new NetworkSubmitPlayerPartyInteractionOption(trade.SessionId, PlayerPartyInteractionOption.Leave, trade.LocalPartyId));
        return true;
    }

    // Sends the local offer when it changed since the last one sent for this trade, and returns whether it sent.
    private bool SendIfChanged()
    {
        var offered = trade.GetLocalOffer();
        // A new trade has sent nothing yet, so an empty offer is no change and any other offer always sends.
        var isNewTrade = lastSent.SessionId != trade.SessionId;
        if (isNewTrade && offered.Count == 0) return false;

        var offer = offerBuilder.Build(trade.SessionId, trade.LocalPartyId, offered);
        var serializedOffer = serializer.Serialize(offer);
        // Compared serialized, so a new offer field can't be missed.
        if (!isNewTrade && serializedOffer.SequenceEqual(lastSent.Offer)) return false;

        network.SendAll(offer);
        lastSent = (trade.SessionId, serializedOffer);
        return true;
    }
}
