using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection loading
/// </summary>
public class LoadingState : ConnectionStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IJoinCampaignBaselineSender campaignBaselineSender;
    private readonly IConnectionMessageQueue connectionMessageQueue;
    private readonly ISendCoalescer coalescer;
    private bool campaignEntryQueued;
    private volatile bool replayMarkerSent;
    private volatile bool baselineSendQueued;
    private volatile int initialBaselinesSent;
    private volatile int finalBaselinesSent;
    private volatile bool finalBaselinePhase;
    private bool finalBarrierQueued;
    private volatile bool worldReadySent;
    private bool catchUpAppliedQueued;

    public LoadingState(
        IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IJoinCampaignBaselineSender campaignBaselineSender,
        IConnectionMessageQueue connectionMessageQueue,
        ISendCoalescer coalescer)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.campaignBaselineSender = campaignBaselineSender;
        this.connectionMessageQueue = connectionMessageQueue;
        this.coalescer = coalescer;

        messageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Subscribe<NetworkJoinReplayApplied>(JoinReplayAppliedHandler);
        messageBroker.Subscribe<NetworkJoinCampaignBaselineRequested>(JoinCampaignBaselineRequestedHandler);
        messageBroker.Subscribe<NetworkJoinCampaignBaselineApplied>(JoinCampaignBaselineAppliedHandler);
        messageBroker.Subscribe<NetworkJoinFinalCampaignBaselineApplied>(JoinFinalCampaignBaselineAppliedHandler);
        messageBroker.Subscribe<NetworkJoinCatchUpApplied>(JoinCatchUpAppliedHandler);
    }

    public override bool IsLoading => true;

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Unsubscribe<NetworkJoinReplayApplied>(JoinReplayAppliedHandler);
        messageBroker.Unsubscribe<NetworkJoinCampaignBaselineRequested>(JoinCampaignBaselineRequestedHandler);
        messageBroker.Unsubscribe<NetworkJoinCampaignBaselineApplied>(JoinCampaignBaselineAppliedHandler);
        messageBroker.Unsubscribe<NetworkJoinFinalCampaignBaselineApplied>(JoinFinalCampaignBaselineAppliedHandler);
        messageBroker.Unsubscribe<NetworkJoinCatchUpApplied>(JoinCatchUpAppliedHandler);
    }

    internal void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId != ConnectionLogic.Peer || campaignEntryQueued) return;

        campaignEntryQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(ConnectionLogic.State, this) == false) return;

            messageBroker.Publish(this, new PlayerCampaignEntered(playerId));
            connectionMessageQueue.Flush(playerId);
            replayMarkerSent = true;
            network.SendImmediate(playerId, new NetworkJoinReplayComplete());
        }, context: nameof(PlayerCampaignEnteredHandler));
    }

    internal void JoinReplayAppliedHandler(MessagePayload<NetworkJoinReplayApplied> obj)
    {
        var playerId = (NetPeer)obj.Who;
        if (playerId != ConnectionLogic.Peer || !replayMarkerSent || initialBaselinesSent != 0) return;

        QueueBaseline(
            playerId,
            isFinalBaseline: false,
            context: nameof(JoinReplayAppliedHandler));
    }

    internal void JoinCampaignBaselineRequestedHandler(
        MessagePayload<NetworkJoinCampaignBaselineRequested> obj)
    {
        var playerId = (NetPeer)obj.Who;
        if (playerId != ConnectionLogic.Peer ||
            initialBaselinesSent == 0 ||
            finalBarrierQueued)
        {
            return;
        }

        QueueBaseline(
            playerId,
            isFinalBaseline: finalBaselinePhase,
            context: nameof(JoinCampaignBaselineRequestedHandler));
    }

    private void QueueBaseline(NetPeer playerId, bool isFinalBaseline, string context)
    {
        if (baselineSendQueued || finalBarrierQueued) return;

        baselineSendQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(ConnectionLogic.State, this) == false)
            {
                baselineSendQueued = false;
                return;
            }

            coalescer.Flush(network);
            connectionMessageQueue.Flush(playerId);
            if (isFinalBaseline)
            {
                finalBaselinesSent++;
            }
            else
            {
                initialBaselinesSent++;
            }
            baselineSendQueued = false;
            campaignBaselineSender.Send(playerId);
        }, context: context);
    }

    internal void JoinCampaignBaselineAppliedHandler(
        MessagePayload<NetworkJoinCampaignBaselineApplied> obj)
    {
        var playerId = (NetPeer)obj.Who;
        if (playerId != ConnectionLogic.Peer ||
            initialBaselinesSent < 2 ||
            baselineSendQueued ||
            finalBaselinePhase ||
            finalBarrierQueued)
        {
            return;
        }

        finalBaselinePhase = true;
        QueueBaseline(
            playerId,
            isFinalBaseline: true,
            context: nameof(JoinCampaignBaselineAppliedHandler));
    }

    internal void JoinFinalCampaignBaselineAppliedHandler(
        MessagePayload<NetworkJoinFinalCampaignBaselineApplied> obj)
    {
        var playerId = (NetPeer)obj.Who;
        if (playerId != ConnectionLogic.Peer ||
            !finalBaselinePhase ||
            finalBaselinesSent == 0 ||
            baselineSendQueued ||
            finalBarrierQueued)
        {
            return;
        }

        finalBarrierQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(ConnectionLogic.State, this) == false) return;

            coalescer.Flush(network);
            worldReadySent = true;
            connectionMessageQueue.OpenWithTail(playerId, new NetworkJoinWorldReady());
        }, context: nameof(JoinFinalCampaignBaselineAppliedHandler));
    }

    internal void JoinCatchUpAppliedHandler(MessagePayload<NetworkJoinCatchUpApplied> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId != ConnectionLogic.Peer ||
            !worldReadySent ||
            catchUpAppliedQueued)
        {
            return;
        }

        catchUpAppliedQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(ConnectionLogic.State, this) == false) return;

            connectionMessageQueue.CompleteCatchUp(playerId);
            ConnectionLogic.EnterCampaign();
        }, context: nameof(JoinCatchUpAppliedHandler));
    }

    public override void CreateCharacter()
    {
    }

    public override void TransferSave()
    {
    }

    public override void Load()
    {
    }

    public override void EnterCampaign()
    {
        ConnectionLogic.SetState<CampaignState>();
    }

    public override void EnterMission()
    {
    }
}
