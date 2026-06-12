using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Receiving Saved Data State
/// </summary>
public class ReceivingSavedDataState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly ILoadingInterface loadingInterface;
    private readonly IGameStateInterface gameStateInterface;

    public ReceivingSavedDataState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        ILoadingInterface loadingInterface,
        IGameStateInterface gameStateInterface) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.loadingInterface = loadingInterface;
        this.gameStateInterface = gameStateInterface;
        messageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);

        // NetworkNewPlayerHeroCreated is handled by the persistent RemotePlayerHeroHandler for the whole client
        // lifetime, so it is captured here AND during LoadingState without a per-state subscription gap.

        // Keep a loading screen up while we receive and load the server world. This is the
        // common state for both new (post character-creation) and returning clients, so the
        // client's local/main-menu view isn't shown during the transition.
        loadingInterface.ShowLoadingScreen(
            "Joining Coop Campaign",
            "Waiting for host save data...");
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
    }

    internal void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
    {
        loadingInterface.SetLoadingMessage(
            "Joining Coop Campaign",
            "Preparing host save data...");

        gameStateInterface.GoToMainMenu();

        var saveData = obj.What.GameSaveData;

        if (saveData == null) return;
        if (saveData.Length == 0) return;

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Loading host save data...");

        gameStateInterface.LoadSaveGame(saveData);

        Logic.LoadSavedData();
    }

    public override void EnterMainMenu()
    {
        gameStateInterface.GoToMainMenu();
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        gameStateInterface.GoToMainMenu();

        Logic.SetState<MainMenuState>();
    }

    public override void ExitGame()
    {
    }

    public override void LoadSavedData()
    {
        Logic.SetState<LoadingState>();
    }

    public override void StartCharacterCreation()
    {
    }

    public override void EnterCampaignState()
    {
    }

    public override void EnterMissionState()
    {
    }

    public override void ValidateModules()
    {
    }
}
