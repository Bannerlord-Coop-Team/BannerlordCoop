using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.UI.Interfaces;
using System.Globalization;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Receiving Saved Data State
/// </summary>
public class ReceivingSavedDataState : ClientStateBase
{
    internal const string SaveHandoffTimedOutText =
        "Loading the host campaign timed out because your game was busy for too long.\n" +
        "Please try joining again.";

    private readonly IMessageBroker messageBroker;
    private readonly ILoadingInterface loadingInterface;
    private readonly IGameStateInterface gameStateInterface;
    private readonly ICoopFinalizer coopFinalizer;

    public ReceivingSavedDataState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        ILoadingInterface loadingInterface,
        IGameStateInterface gameStateInterface,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.loadingInterface = loadingInterface;
        this.gameStateInterface = gameStateInterface;
        this.coopFinalizer = coopFinalizer;
        messageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
        messageBroker.Subscribe<NetworkGameSaveDataProgress>(Handle_NetworkGameSaveDataProgress);

        loadingInterface.ShowLoadingScreen(
            "Joining Coop Campaign",
            "Waiting for host save data...");
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
        messageBroker.Unsubscribe<NetworkGameSaveDataProgress>(Handle_NetworkGameSaveDataProgress);
    }

    internal void Handle_NetworkGameSaveDataProgress(MessagePayload<NetworkGameSaveDataProgress> obj)
    {
        int remaining = obj.What.PacketsRemaining;
        string description = remaining > 0
            ? "Waiting for host save data... " +
              remaining.ToString("N0", CultureInfo.InvariantCulture) +
              " save packets remaining"
            : "Host save data received.";

        loadingInterface.SetLoadingMessage(
            "Joining Coop Campaign",
            description);
    }

    internal void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
    {
        loadingInterface.SetLoadingMessage(
            "Joining Coop Campaign",
            "Preparing host save data...");

        // Keeps the usual wait. An expired EndGame is queued again ahead of the load below, so it still runs first
        gameStateInterface.GoToMainMenu();

        var saveData = obj.What.GameSaveData;

        if (saveData == null) return;
        if (saveData.Length == 0) return;

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Loading host save data...");

        // The first map frame after character creation can keep the game thread busy for over a minute on a big map
        if (!gameStateInterface.TryLoadSaveData(saveData, NetworkJoinLimits.SaveHandoffTimeout))
        {
            // Back to the main menu with a message instead of an endless loading screen. GoToMainMenu above
            // already queued leaving the campaign, and the finalizer ends the session and disconnects.
            coopFinalizer.Finalize(SaveHandoffTimedOutText);
            return;
        }

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
