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
    private volatile bool baselineResponseExpected;
    private volatile bool finalBaselineResponseExpected;
    private int successfulBaselines;
    private volatile bool waitingForTimeCatchUp;
    private volatile bool finalTimeCatchUp;
    private volatile bool timeCatchUpCheckQueued;
    private volatile bool waitingForWorldReady;
    private volatile bool joinCompletionQueued;

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
            messageBroker.Subscribe<NetworkJoinWorldReady>(Handle_JoinWorldReady);
        }

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Creating remote player heroes...");

        // Tell the server the campaign exists locally so it can begin the ordered replay and baselines.
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
            messageBroker.Unsubscribe<NetworkJoinWorldReady>(Handle_JoinWorldReady);
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

            baselineResponseExpected = true;
            network.SendAll(new NetworkJoinReplayApplied());
        }, context: nameof(Handle_JoinReplayComplete));
    }

    internal void Handle_JoinCampaignBaselineApplied(MessagePayload<JoinCampaignBaselineApplied> obj)
    {
        if (!baselineResponseExpected || waitingForTimeCatchUp || waitingForWorldReady) return;

        bool isFinalBaseline = finalBaselineResponseExpected;
        baselineResponseExpected = false;
        finalBaselineResponseExpected = false;

        loadingInterface.SetLoadingMessage(
            "Loading Host Campaign",
            "Catching up to the host...");

        if (!obj.What.Success)
        {
            RequestBaseline(isFinalBaseline);
            return;
        }

        if (isFinalBaseline)
        {
            waitingForTimeCatchUp = true;
            finalTimeCatchUp = true;
            return;
        }

        successfulBaselines++;
        if (successfulBaselines < 2)
        {
            RequestBaseline(isFinalBaseline: false);
            return;
        }

        waitingForTimeCatchUp = true;
        finalTimeCatchUp = false;
    }

    private void RequestBaseline(bool isFinalBaseline)
    {
        waitingForTimeCatchUp = false;
        finalTimeCatchUp = false;
        mapTimeTrackerInterface.ResetForCampaignJoin();
        baselineResponseExpected = true;
        finalBaselineResponseExpected = isFinalBaseline;
        network.SendAll(new NetworkJoinCampaignBaselineRequested());
    }

    internal void Handle_CampaignTimeSampleReceived(MessagePayload<CampaignTimeSampleReceived> obj)
    {
        if (!waitingForTimeCatchUp || timeCatchUpCheckQueued) return;

        timeCatchUpCheckQueued = true;
        GameThread.RunSafe(() =>
        {
            try
            {
                if (ReferenceEquals(Logic.State, this) == false) return;
                if (!mapTimeTrackerInterface.TryCompleteCampaignJoinCatchUp(
                    out bool baselineRefreshRequired))
                {
                    return;
                }

                if (baselineRefreshRequired)
                {
                    RequestBaseline(finalTimeCatchUp);
                    return;
                }

                waitingForTimeCatchUp = false;
                if (finalTimeCatchUp)
                {
                    finalTimeCatchUp = false;
                    waitingForWorldReady = true;
                    network.SendAll(new NetworkJoinFinalCampaignBaselineApplied());
                    return;
                }

                mapTimeTrackerInterface.ResetForCampaignJoin();
                baselineResponseExpected = true;
                finalBaselineResponseExpected = true;
                network.SendAll(new NetworkJoinCampaignBaselineApplied());
            }
            finally
            {
                timeCatchUpCheckQueued = false;
            }
        }, context: nameof(Handle_CampaignTimeSampleReceived));
    }

    internal void Handle_JoinWorldReady(MessagePayload<NetworkJoinWorldReady> obj)
    {
        if (!waitingForWorldReady || joinCompletionQueued) return;

        joinCompletionQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(Logic.State, this) == false) return;

            CompleteCampaignEntry();
            network.SendAll(new NetworkJoinCatchUpApplied());
        }, context: nameof(Handle_JoinWorldReady));
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
