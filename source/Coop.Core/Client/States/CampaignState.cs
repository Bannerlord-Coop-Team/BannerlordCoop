// Ignore Spelling: Finalizer

using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
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
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;
    private readonly bool waitingForJoinCatchUp;
    private bool replayAppliedQueued;
    private volatile bool baselineApplied;
    private volatile bool joinCompletionQueued;
    private bool hasRequestedBaselineRefresh;

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
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;
        waitingForJoinCatchUp = logic.State is LoadingState;

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<MissionStateEntered>(Handle_MissionStateEntered);
        if (waitingForJoinCatchUp)
        {
            mapTimeTrackerInterface.ResetForCampaignJoin();
            messageBroker.Subscribe<NetworkJoinReplayComplete>(Handle_JoinReplayComplete);
            messageBroker.Subscribe<JoinCampaignBaselineApplied>(Handle_JoinCampaignBaselineApplied);
            messageBroker.Subscribe<CampaignTimeSampleReceived>(Handle_CampaignTimeSampleReceived);
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
            messageBroker.Unsubscribe<NetworkJoinReplayComplete>(Handle_JoinReplayComplete);
            messageBroker.Unsubscribe<JoinCampaignBaselineApplied>(Handle_JoinCampaignBaselineApplied);
            messageBroker.Unsubscribe<CampaignTimeSampleReceived>(Handle_CampaignTimeSampleReceived);
        }
    }

    internal void Handle_JoinReplayComplete(MessagePayload<NetworkJoinReplayComplete> obj)
    {
        if (replayAppliedQueued) return;

        replayAppliedQueued = true;
        // The marker is ordered after the replay; this action runs behind every earlier apply action.
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(Logic.State, this) == false) return;

            network.SendAll(new NetworkJoinReplayApplied());
        }, context: nameof(Handle_JoinReplayComplete));
    }

    internal void Handle_JoinCampaignBaselineApplied(MessagePayload<JoinCampaignBaselineApplied> obj)
    {
        if (baselineApplied) return;

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Catching up to the host...");

        if (!hasRequestedBaselineRefresh)
        {
            hasRequestedBaselineRefresh = true;
            mapTimeTrackerInterface.ResetForCampaignJoin();
            network.SendAll(new NetworkJoinCampaignBaselineRequested());
            return;
        }

        baselineApplied = true;
    }

    internal void Handle_CampaignTimeSampleReceived(MessagePayload<CampaignTimeSampleReceived> obj)
    {
        if (!baselineApplied || joinCompletionQueued) return;

        joinCompletionQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(Logic.State, this) == false) return;

            if (!mapTimeTrackerInterface.TryCompleteCampaignJoinCatchUp(out bool baselineRefreshRequired))
            {
                joinCompletionQueued = false;
                return;
            }

            if (baselineRefreshRequired)
            {
                baselineApplied = false;
                joinCompletionQueued = false;
                mapTimeTrackerInterface.ResetForCampaignJoin();
                network.SendAll(new NetworkJoinCampaignBaselineRequested());
                return;
            }

            CompleteCampaignEntry();
            network.SendAll(new NetworkJoinCatchUpApplied());
        }, context: nameof(Handle_CampaignTimeSampleReceived));
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
