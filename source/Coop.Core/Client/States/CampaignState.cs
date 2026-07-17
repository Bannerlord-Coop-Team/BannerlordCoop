// Ignore Spelling: Finalizer

using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Time.Interfaces;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State controller for campaign client state
/// </summary>
public class CampaignState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly ILoadingInterface loadingInterface;
    private readonly IGameStateInterface gameStateInterface;
    private readonly ICoopFinalizer coopFinalizer;
    private readonly INetwork network;
    private readonly bool waitingForJoinCatchUp;

    public CampaignState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        ILoadingInterface loadingInterface,
        IGameStateInterface gameStateInterface,
        ICoopFinalizer coopFinalizer,
        IMapTimeTrackerInterface mapTimeTrackerInterface) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.loadingInterface = loadingInterface;
        this.gameStateInterface = gameStateInterface;
        this.coopFinalizer = coopFinalizer;
        this.network = network;
        waitingForJoinCatchUp = logic.State is LoadingState;

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<MissionStateEntered>(Handle_MissionStateEntered);
        if (waitingForJoinCatchUp)
        {
            mapTimeTrackerInterface.ResetForCampaignJoin();
            messageBroker.Subscribe<NetworkJoinCatchUpComplete>(Handle_JoinCatchUpComplete);
        }

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Creating remote player heroes...");

        // Tell the server we have fully entered the campaign so it flushes the broadcasts it withheld
        // for us (the per-peer ConnectionMessageQueue) and resumes sending the live world stream.
        network.SendAll(new NetworkPlayerCampaignEntered());

        if (!waitingForJoinCatchUp)
        {
            CompleteCampaignEntry();
        }
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<MissionStateEntered>(Handle_MissionStateEntered);
        if (waitingForJoinCatchUp)
        {
            messageBroker.Unsubscribe<NetworkJoinCatchUpComplete>(Handle_JoinCatchUpComplete);
        }
    }

    internal void Handle_JoinCatchUpComplete(MessagePayload<NetworkJoinCatchUpComplete> obj)
    {
        // ReliableOrdered delivery publishes this after every held update. Deferring once more puts
        // the release behind the game-thread actions those earlier handlers queued.
        GameThread.RunSafe(() =>
        {
            CompleteCampaignEntry();
            network.SendAll(new NetworkJoinCatchUpApplied());
        }, context: nameof(Handle_JoinCatchUpComplete));
    }

    private void CompleteCampaignEntry()
    {
        messageBroker.Publish(this, new PlayerKillFeedColorResendRequested());
        loadingInterface.HideLoadingScreen();
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
