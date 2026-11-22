namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Shows the trade overlay with both players' acceptance while a player trade is open.
internal interface IPlayerPartyTradeOverlayInterface
{
    // Shows the overlay for a trade with the named player.
    void Show(string sessionId, string otherPlayerName);

    // Hides the overlay, unless it shows another trade.
    void Hide(string sessionId);

    // Shows whether each player accepted the current offers.
    void UpdateState(bool localAccepted, bool remoteAccepted);
}

// Forwards to the overlay's global layer, so the trade code reaches it through DI instead of the static instance.
internal class PlayerPartyTradeOverlayInterface : IPlayerPartyTradeOverlayInterface
{
    public void Show(string sessionId, string otherPlayerName)
        => PlayerPartyTradeOverlay.Instance.Show(sessionId, otherPlayerName);

    public void Hide(string sessionId)
        => PlayerPartyTradeOverlay.Instance.Hide(sessionId);

    public void UpdateState(bool localAccepted, bool remoteAccepted)
        => PlayerPartyTradeOverlay.Instance.UpdateState(localAccepted, remoteAccepted);
}
