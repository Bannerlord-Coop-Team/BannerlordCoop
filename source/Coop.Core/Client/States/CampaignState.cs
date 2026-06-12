// Ignore Spelling: Finalizer

using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Client.States;

/// <summary>
/// State controller for campaign client state
/// </summary>
public class CampaignState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ILoadingInterface loadingInterface;
    private readonly IGameStateInterface gameStateInterface;
    private readonly ICoopFinalizer coopFinalizer;

    public CampaignState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        ILoadingInterface loadingInterface,
        IGameStateInterface gameStateInterface,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.loadingInterface = loadingInterface;
        this.gameStateInterface = gameStateInterface;
        this.coopFinalizer = coopFinalizer;

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<MissionStateEntered>(Handle_MissionStateEntered);
    }

    public override void Enter()
    {
        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Creating remote player heroes...");

        // Campaign is fully loaded and our hero is switched in. Signal the persistent RemotePlayerHeroHandler so
        // it drains any remote heroes deferred during loading and starts instantiating further ones immediately.
        // (Remote-hero creation lives in that handler so the NetworkNewPlayerHeroCreated message is never dropped
        // in the gap between the loading states.)
        messageBroker.Publish(this, new ClientCampaignEntered());

        network.SendAll(new NetworkPlayerCampaignEntered());

        loadingInterface.HideLoadingScreen();
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<MissionStateEntered>(Handle_MissionStateEntered);
    }

    internal void Handle_MissionStateEntered(MessagePayload<MissionStateEntered> obj)
    {
        Logic.SetState<MissionState>();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        coopFinalizer.Finalize("Client has been stopped");
    }

    public override void EnterMissionState()
    {
        // Mission state may be removed in the future
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
    }

    public override void ExitGame()
    {
    }

    public override void LoadSavedData()
    {
    }

    public override void StartCharacterCreation()
    {
    }

    public override void EnterCampaignState()
    {
    }

    public override void ValidateModules()
    {
    }
}
