using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal class PlayerPartyTradeOverlay : GlobalLayer
{
    private static PlayerPartyTradeOverlay instance;

    private PlayerPartyTradeOverlayVM dataSource;
    private GauntletLayer gauntletLayer;
    private string sessionId;
    private bool isShown;

    public static PlayerPartyTradeOverlay Instance => instance ??= new PlayerPartyTradeOverlay();

    public void Show(string activeSessionId, string otherPlayerName)
    {
        if (isShown && sessionId == activeSessionId)
            return;

        Hide();

        sessionId = activeSessionId;
        dataSource = new PlayerPartyTradeOverlayVM(otherPlayerName);
        gauntletLayer = new GauntletLayer("CoopPlayerPartyTradeOverlay", 1000);
        gauntletLayer.LoadMovie("CoopPlayerPartyTradeOverlay", dataSource);
        Layer = gauntletLayer;

        ScreenManager.AddGlobalLayer(this, false);
        isShown = true;
    }

    public void Hide(string activeSessionId = null)
    {
        if (!isShown) return;
        if (activeSessionId != null && sessionId != activeSessionId) return;

        ScreenManager.RemoveGlobalLayer(this);
        gauntletLayer = null;
        dataSource = null;
        sessionId = null;
        isShown = false;
    }

    public void UpdateState(bool localAccepted, bool remoteAccepted)
    {
        if (!isShown || dataSource == null) return;

        dataSource.LocalAccepted = localAccepted;
        dataSource.RemoteAccepted = remoteAccepted;
    }
}

internal class PlayerPartyTradeOverlayVM : ViewModel
{
    private bool localAccepted;
    private bool remoteAccepted;

    public PlayerPartyTradeOverlayVM(string otherPlayerName)
    {
        OtherPlayerName = otherPlayerName;
    }

    public string OtherPlayerName { get; }

    [DataSourceProperty]
    public bool LocalAccepted
    {
        get => localAccepted;
        set
        {
            if (localAccepted == value) return;
            localAccepted = value;
            OnPropertyChangedWithValue(value, nameof(LocalAccepted));
            OnPropertyChanged(nameof(LocalAcceptedText));
        }
    }

    [DataSourceProperty]
    public bool RemoteAccepted
    {
        get => remoteAccepted;
        set
        {
            if (remoteAccepted == value) return;
            remoteAccepted = value;
            OnPropertyChangedWithValue(value, nameof(RemoteAccepted));
            OnPropertyChanged(nameof(RemoteAcceptedText));
        }
    }

    [DataSourceProperty]
    public string LocalAcceptedText => LocalAccepted ? "You have accepted" : "You have not accepted";

    [DataSourceProperty]
    public string RemoteAcceptedText => RemoteAccepted ? $"{OtherPlayerName} has accepted" : $"{OtherPlayerName} has not accepted";
}
