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

    // The queued flush always clears the pending flag and a failure is kept per trade, so neither carries into the next trade.
    public void Flush()
    {
        if (!changePending && !HasUnsentOffer) return;
        changePending = false;
        if (!trade.IsActive) return;

        try
        {
            SendIfChanged();
            failedSendSessionId = null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send the player-party trade offer");
            failedSendSessionId = trade.SessionId;
        }
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

    // Sends the local offer when it changed since the last one sent for this trade.
    private void SendIfChanged()
    {
        var offered = trade.GetLocalOffer();
        var isNewTrade = lastSent.SessionId != trade.SessionId;
        // Nothing was sent for a new trade yet, so it compares against an empty offer and a stack the last trade ended on still counts as a change.
        if (isNewTrade && offered.Count == 0) return;

        var offer = offerBuilder.Build(trade.SessionId, trade.LocalPartyId, offered);
        var serializedOffer = serializer.Serialize(offer);
        // Compared serialized, so a new offer field can't be missed.
        if (!isNewTrade && serializedOffer.SequenceEqual(lastSent.Offer)) return;

        network.SendAll(offer);
        lastSent = (trade.SessionId, serializedOffer);
        // Clears both acceptances like the server does.
        trade.ResetAcceptance();
    }
}
