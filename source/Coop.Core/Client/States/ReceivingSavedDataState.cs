using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Receiving Saved Data State
/// </summary>
public class ReceivingSavedDataState : ClientStateBase
{
    private NetworkGameSaveDataReceived saveDataMessage = default;
    private readonly IMessageBroker messageBroker;
    private readonly ILoadingInterface loadingInterface;

    public ReceivingSavedDataState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        ILoadingInterface loadingInterface) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.loadingInterface = loadingInterface;

        messageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
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
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    internal void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
    {
        saveDataMessage = obj.What;
        loadingInterface.SetLoadingMessage(
            "Joining Coop Campaign",
            "Preparing host save data...");
        Logic.EnterMainMenu();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        var saveData = saveDataMessage?.GameSaveData;

        if (saveData == null) return;
        if (saveData.Length == 0) return;

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Loading host save data...");

        var commandLoad = new LoadGameSave(saveData);
        messageBroker.Publish(this, commandLoad);

        Logic.LoadSavedData();
    }

    public override void EnterMainMenu()
    {
        messageBroker.Publish(this, new EnterMainMenu());
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        messageBroker.Publish(this, new EnterMainMenu());

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
