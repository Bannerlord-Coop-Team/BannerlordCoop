using Common;
using Common.Logging;
using Common.Network;
using Common.Serialization;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Coalesces the local trade offer into one send per game-thread drain, because vanilla recomputes it after every item change.
internal interface IPlayerPartyTradeOfferSender
{
    // Starts sending offers for this trade, compared against nothing offered.
    void Begin(string sessionId, string partyId);

    // Stops sending offers when the trade that ended is the one being sent for.
    void End(string sessionId);

    // Records that the offer on this barter screen may have changed and schedules a flush for it.
    void MarkChanged(BarterVM barterVM);

    // Sends the pending offer when it differs from the last one sent; false when sending it failed.
    bool Flush();
}

// Scoped per client, because the barter patches and the trade handler must share one pending offer.
internal class PlayerPartyTradeOfferSender : IPlayerPartyTradeOfferSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyTradeOfferSender>();

    private readonly INetwork network;
    private readonly ICommonSerializer serializer;
    private readonly IPlayerPartyTradeOfferBuilder offerBuilder;

    private string sessionId;
    private string partyId;
    private BarterVM changedBarterVM;
    private byte[] lastSentOffer;

    public PlayerPartyTradeOfferSender(INetwork network, ICommonSerializer serializer, IPlayerPartyTradeOfferBuilder offerBuilder)
    {
        this.network = network;
        this.serializer = serializer;
        this.offerBuilder = offerBuilder;
    }

    // The baseline is the empty offer for this trade, so a stack the last trade ended on still counts as a change.
    public void Begin(string sessionId, string partyId)
    {
        this.sessionId = sessionId;
        this.partyId = partyId;
        changedBarterVM = null;
        lastSentOffer = serializer.Serialize(offerBuilder.Build(sessionId, partyId, Array.Empty<Barterable>()));
    }

    // Matches the trade context, which ignores the end of any other session.
    public void End(string sessionId)
    {
        if (this.sessionId != sessionId) return;

        this.sessionId = null;
        partyId = null;
        changedBarterVM = null;
    }

    // An NPC barter is settled locally, so it never schedules a send.
    public void MarkChanged(BarterVM barterVM)
    {
        if (sessionId == null) return;

        var flushScheduled = changedBarterVM != null;
        changedBarterVM = barterVM;
        if (flushScheduled) return;

        GameThread.EnqueueSafe(() => Flush(), context: "Send player-party trade offer");
    }

    // Clears the pending change before sending, so a failing offer logs once instead of on every flush.
    public bool Flush()
    {
        var barterVM = changedBarterVM;
        if (barterVM == null) return true;
        changedBarterVM = null;

        try
        {
            SendIfChanged(barterVM);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send the player-party trade offer");
            return false;
        }
    }

    // Compares the serialized offer, so a new offer field can't be missed, and a sent offer clears both acceptances like the server does.
    private void SendIfChanged(BarterVM barterVM)
    {
        var offer = offerBuilder.Build(sessionId, partyId, GetLocalOffer(barterVM));
        var serializedOffer = serializer.Serialize(offer);
        if (serializedOffer.SequenceEqual(lastSentOffer)) return;

        network.SendAll(offer);
        lastSentOffer = serializedOffer;
        PlayerPartyTradeContext.ResetAcceptance();
    }

    // Reads both offer columns because the local party can be on either side, keeping what the context lets it offer.
    private static Barterable[] GetLocalOffer(BarterVM barterVM)
        => barterVM.LeftOfferList
            .Concat(barterVM.RightOfferList)
            .Select(item => item.Barterable)
            .Where(PlayerPartyTradeContext.CanOffer)
            .ToArray();
}
