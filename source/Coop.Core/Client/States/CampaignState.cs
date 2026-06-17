// Ignore Spelling: Finalizer

using Common.Messaging;
using Common.Network;
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
        this.gameStateInterface = gameStateInterface;
        this.coopFinalizer = coopFinalizer;

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<MissionStateEntered>(Handle_MissionStateEntered);

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Creating remote player heroes...");

        // Tell the server we have fully entered the campaign so it flushes the broadcasts it withheld
        // for us (the per-peer ConnectionMessageQueue) and resumes sending the live world stream.
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
