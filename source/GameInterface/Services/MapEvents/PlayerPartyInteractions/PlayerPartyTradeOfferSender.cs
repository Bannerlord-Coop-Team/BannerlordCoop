using Common;
using Common.Logging;
using Common.Network;
using Common.Serialization;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Coalesces the local trade offer into one send per game-thread drain, because vanilla recomputes it after every item change.
internal interface IPlayerPartyTradeOfferSender
{
    // Records that the local offer may have changed and schedules one flush for the burst.
    void MarkChanged();

    // Sends the pending offer when it differs from the last one sent; false when sending it failed.
    bool Flush();
}

// Keeps only the coalescing state, the trade session itself belongs to the trade context.
internal class PlayerPartyTradeOfferSender : IPlayerPartyTradeOfferSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyTradeOfferSender>();

    private readonly INetwork network;
    private readonly ICommonSerializer serializer;
    private readonly IPlayerPartyTradeOfferBuilder offerBuilder;
    private readonly IPlayerPartyTradeContext trade;

    private bool changePending;
    private string baselineSessionId;
    private byte[] lastSentOffer;

    public PlayerPartyTradeOfferSender(
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

    // An NPC barter is settled locally, so it never schedules a send.
    public void MarkChanged()
    {
        if (!trade.IsActive) return;

        ResetIfNewTrade();
        if (changePending) return;

        changePending = true;
        GameThread.EnqueueSafe(() => Flush(), context: "Send player-party trade offer");
    }

    // Clears the pending change before sending, so a failing offer logs once instead of on every flush.
    public bool Flush()
    {
        if (!trade.IsActive) return true;

        ResetIfNewTrade();
        if (!changePending) return true;
        changePending = false;

        try
        {
            SendIfChanged();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send the player-party trade offer");
            return false;
        }
    }

    // A new trade starts from an empty offer, so a stack the last trade ended on still counts as a change.
    private void ResetIfNewTrade()
    {
        if (baselineSessionId == trade.SessionId) return;

        baselineSessionId = trade.SessionId;
        lastSentOffer = serializer.Serialize(offerBuilder.Build(trade.SessionId, trade.LocalPartyId, Array.Empty<Barterable>()));
        changePending = false;
    }

    // Compares the serialized offer, so a new offer field can't be missed, and a sent offer clears both acceptances like the server does.
    private void SendIfChanged()
    {
        var offer = offerBuilder.Build(trade.SessionId, trade.LocalPartyId, trade.GetLocalOffer());
        var serializedOffer = serializer.Serialize(offer);
        if (serializedOffer.SequenceEqual(lastSentOffer)) return;

        network.SendAll(offer);
        lastSentOffer = serializedOffer;
        trade.ResetAcceptance();
    }
}
