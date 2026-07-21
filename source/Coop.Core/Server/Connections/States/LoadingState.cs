using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>State representing a connection loading the campaign.</summary>
public class LoadingState : ConnectionStateBase
{
    private enum JoinPhase
    {
        WaitingForCampaignEntry,
        CampaignEntryQueued,
        WaitingForReplayApplied,
        InitialBaselineQueued,
        WaitingForInitialBaseline,
        FinalBaselineQueued,
        WaitingForFinalBaseline,
        WorldReadyQueued,
        WaitingForCatchUpApplied,
        CatchUpAppliedQueued,
    }

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IJoinCampaignBaselineSender campaignBaselineSender;
    private readonly IConnectionMessageQueue connectionMessageQueue;
    private readonly ISendCoalescer coalescer;
    private volatile JoinPhase phase;
    private int initialBaselinesSent;

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
        messageBroker.Subscribe<NetworkJoinSync>(JoinSyncHandler);
    }

    public override bool IsLoading => true;

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Unsubscribe<NetworkJoinSync>(JoinSyncHandler);
    }

    internal void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> payload)
    {
        var peer = (NetPeer)payload.Who;
        if (peer != ConnectionLogic.Peer || phase != JoinPhase.WaitingForCampaignEntry) return;

        phase = JoinPhase.CampaignEntryQueued;
        GameThread.RunSafe(() =>
        {
            if (!IsCurrent(JoinPhase.CampaignEntryQueued)) return;

            messageBroker.Publish(this, new PlayerCampaignEntered(peer));
            connectionMessageQueue.Flush(peer);
            phase = JoinPhase.WaitingForReplayApplied;
            network.SendImmediate(peer, new NetworkJoinSync(JoinSyncSignal.ReplayComplete));
        }, context: nameof(PlayerCampaignEnteredHandler));
    }

    internal void JoinSyncHandler(MessagePayload<NetworkJoinSync> payload)
    {
        var peer = (NetPeer)payload.Who;
        if (peer != ConnectionLogic.Peer) return;

        switch (payload.What.Signal)
        {
            case JoinSyncSignal.ReplayApplied when phase == JoinPhase.WaitingForReplayApplied:
                QueueBaseline(peer, isFinal: false, nameof(JoinSyncSignal.ReplayApplied));
                break;
            case JoinSyncSignal.BaselineRequested when phase == JoinPhase.WaitingForInitialBaseline:
                QueueBaseline(peer, isFinal: false, nameof(JoinSyncSignal.BaselineRequested));
                break;
            case JoinSyncSignal.BaselineRequested when phase == JoinPhase.WaitingForFinalBaseline:
                QueueBaseline(peer, isFinal: true, nameof(JoinSyncSignal.BaselineRequested));
                break;
            case JoinSyncSignal.BaselineApplied
                when phase == JoinPhase.WaitingForInitialBaseline && initialBaselinesSent >= 2:
                QueueBaseline(peer, isFinal: true, nameof(JoinSyncSignal.BaselineApplied));
                break;
            case JoinSyncSignal.FinalBaselineApplied when phase == JoinPhase.WaitingForFinalBaseline:
                QueueWorldReady(peer);
                break;
            case JoinSyncSignal.CatchUpApplied when phase == JoinPhase.WaitingForCatchUpApplied:
                QueueCampaignEntry(peer);
                break;
        }
    }

    private void QueueBaseline(NetPeer peer, bool isFinal, string context)
    {
        JoinPhase queued = isFinal ? JoinPhase.FinalBaselineQueued : JoinPhase.InitialBaselineQueued;
        JoinPhase waiting = isFinal ? JoinPhase.WaitingForFinalBaseline : JoinPhase.WaitingForInitialBaseline;
        phase = queued;

        GameThread.RunSafe(() =>
        {
            if (!IsCurrent(queued)) return;

            coalescer.Flush(network);
            connectionMessageQueue.Flush(peer);
            if (!isFinal) initialBaselinesSent++;
            phase = waiting;
            campaignBaselineSender.Send(peer);
        }, context: context);
    }

    private void QueueWorldReady(NetPeer peer)
    {
        phase = JoinPhase.WorldReadyQueued;
        GameThread.RunSafe(() =>
        {
            if (!IsCurrent(JoinPhase.WorldReadyQueued)) return;

            coalescer.Flush(network);
            phase = JoinPhase.WaitingForCatchUpApplied;
            connectionMessageQueue.OpenWithTail(
                peer,
                new NetworkJoinSync(JoinSyncSignal.WorldReady));
        }, context: nameof(JoinSyncSignal.FinalBaselineApplied));
    }

    private void QueueCampaignEntry(NetPeer peer)
    {
        phase = JoinPhase.CatchUpAppliedQueued;
        GameThread.RunSafe(() =>
        {
            if (!IsCurrent(JoinPhase.CatchUpAppliedQueued)) return;

            connectionMessageQueue.CompleteCatchUp(peer);
            ConnectionLogic.EnterCampaign();
        }, context: nameof(JoinSyncSignal.CatchUpApplied));
    }

    private bool IsCurrent(JoinPhase expected) =>
        ReferenceEquals(ConnectionLogic.State, this) && phase == expected;

    public override void CreateCharacter()
    {
    }

    public override void TransferSave()
    {
    }

    public override void Load()
    {
    }

    public override void EnterCampaign() => ConnectionLogic.SetState<CampaignState>();

    public override void EnterMission()
    {
    }
}
