using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties;
using Coop.Core.Server.Services.Kingdoms;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Threading;

namespace Coop.Core.Server.Connections.States;

/// <summary>State representing a connection loading the campaign.</summary>
public class LoadingState : ConnectionStateBase
{
    internal const int MaxBaselinesPerJoin = 16;

    private static readonly ILogger Logger = LogManager.GetLogger<LoadingState>();

    private enum JoinPhase
    {
        WaitingForCampaignEntry,
        CampaignEntryQueued,
        ReplayBatchQueued,
        WaitingForReplayBatchApplied,
        WaitingForReplayApplied,
        InitialBaselineQueued,
        WaitingForInitialBaseline,
        FinalBaselineQueued,
        WaitingForFinalBaseline,
        WorldReadyQueued,
        WaitingForCatchUpApplied,
        CatchUpAppliedQueued,
        Aborted,
    }

    private enum ReplayContinuation
    {
        InitialReplay,
        InitialBaseline,
        FinalBaseline,
        WorldReady,
    }

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IJoinCampaignBaselineSender campaignBaselineSender;
    private readonly IJoinCampaignKingdomBaseLineSender campaignKingdomBaselineSender;
    private readonly IConnectionMessageQueue connectionMessageQueue;
    private readonly ISendCoalescer coalescer;
    private readonly IPlayerManager playerManager;
    private readonly object joinGate = new object();
    private int phase = (int)JoinPhase.WaitingForCampaignEntry;
    private int initialBaselinesSent;
    private int totalBaselinesSent;
    private int baselineLimitLogged;
    private int nextReplayBatchId;
    private readonly Queue<(int Id, int Bytes)> replayBatches = new Queue<(int, int)>();
    private bool replayDrainBlocked;
    private long joinProgressVersion;
    private ReplayContinuation replayContinuation;
#if DEBUG

    internal string DebugJoinState =>
        $"phase={CurrentPhase} initialBaselinesSent={initialBaselinesSent} " +
        $"totalBaselinesSent={totalBaselinesSent} nextReplayBatchId={nextReplayBatchId} " +
        $"joinProgressVersion={JoinProgressVersion} joinCatchUpPending={IsJoinCatchUpPending}";
#endif

    public LoadingState(
        IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IJoinCampaignBaselineSender campaignBaselineSender,
        IJoinCampaignKingdomBaseLineSender campaignKingdomBaselineSender,
        IConnectionMessageQueue connectionMessageQueue,
        ISendCoalescer coalescer,
        IPlayerManager playerManager)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.campaignBaselineSender = campaignBaselineSender;
        this.campaignKingdomBaselineSender = campaignKingdomBaselineSender;
        this.connectionMessageQueue = connectionMessageQueue;
        this.coalescer = coalescer;
        this.playerManager = playerManager;

        messageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Subscribe<NetworkJoinSync>(JoinSyncHandler);
    }

    public override bool IsLoading => true;

    internal bool IsJoinCatchUpPending => IsPendingJoinPhase(CurrentPhase);

    internal bool IsPreEntryPending => CurrentPhase <= JoinPhase.CampaignEntryQueued;

    internal bool IsWaitingForReplayApplied =>
        CurrentPhase == JoinPhase.WaitingForReplayApplied;

    internal bool IsAborted => CurrentPhase == JoinPhase.Aborted;

    internal string JoinPhaseName => CurrentPhase.ToString();

    internal long JoinProgressVersion => Interlocked.Read(ref joinProgressVersion);

    internal bool TryAbortJoinCatchUp()
    {
        if (!Monitor.TryEnter(joinGate)) return false;
        try
        {
            while (ReferenceEquals(ConnectionLogic.State, this))
            {
                JoinPhase current = CurrentPhase;
                if (!IsPendingJoinPhase(current)) return false;
                if (Interlocked.CompareExchange(
                        ref phase,
                        (int)JoinPhase.Aborted,
                        (int)current) == (int)current)
                {
                    replayBatches.Clear();
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Monitor.Exit(joinGate);
        }
    }

    internal bool TryAbortPreEntryJoin()
    {
        if (!Monitor.TryEnter(joinGate)) return false;
        try
        {
            while (ReferenceEquals(ConnectionLogic.State, this))
            {
                JoinPhase current = CurrentPhase;
                if (current > JoinPhase.CampaignEntryQueued) return false;
                if (Interlocked.CompareExchange(
                        ref phase,
                        (int)JoinPhase.Aborted,
                        (int)current) == (int)current)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Monitor.Exit(joinGate);
        }
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Unsubscribe<NetworkJoinSync>(JoinSyncHandler);
    }

    internal void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> payload)
    {
        var peer = (NetPeer)payload.Who;
        if (!ReferenceEquals(peer, ConnectionLogic.Peer) ||
            !TrySetPhase(JoinPhase.WaitingForCampaignEntry, JoinPhase.CampaignEntryQueued))
        {
            return;
        }

        GameThread.RunSafe(() =>
        {
            lock (joinGate)
            {
                if (!IsCurrent(JoinPhase.CampaignEntryQueued)) return;

                messageBroker.Publish(this, new PlayerCampaignEntered(peer));
                messageBroker.Publish(this, new PlayerConnectionStateChanged());
                replayContinuation = ReplayContinuation.InitialReplay;
                if (!TrySetPhase(JoinPhase.CampaignEntryQueued, JoinPhase.ReplayBatchQueued)) return;
                PumpReplay(peer);
            }
        }, context: nameof(PlayerCampaignEnteredHandler));
    }

    internal void JoinSyncHandler(MessagePayload<NetworkJoinSync> payload)
    {
        var peer = (NetPeer)payload.Who;
        if (!ReferenceEquals(peer, ConnectionLogic.Peer)) return;

        switch (payload.What.Signal)
        {
            case JoinSyncSignal.ReplayBatchApplied:
                QueueNextReplayBatch(peer, payload.What.ReplayBatchId);
                break;
            case JoinSyncSignal.ReplayApplied:
                QueueBaseline(
                    peer,
                    JoinPhase.WaitingForReplayApplied,
                    isFinal: false,
                    nameof(JoinSyncSignal.ReplayApplied));
                break;
            case JoinSyncSignal.BaselineRequested when CurrentPhase == JoinPhase.WaitingForInitialBaseline:
                QueueBaseline(
                    peer,
                    JoinPhase.WaitingForInitialBaseline,
                    isFinal: false,
                    nameof(JoinSyncSignal.BaselineRequested));
                break;
            case JoinSyncSignal.BaselineRequested when CurrentPhase == JoinPhase.WaitingForFinalBaseline:
                QueueBaseline(
                    peer,
                    JoinPhase.WaitingForFinalBaseline,
                    isFinal: true,
                    nameof(JoinSyncSignal.BaselineRequested));
                break;
            case JoinSyncSignal.BaselineApplied
                when CurrentPhase == JoinPhase.WaitingForInitialBaseline && initialBaselinesSent >= 2:
                QueueBaseline(
                    peer,
                    JoinPhase.WaitingForInitialBaseline,
                    isFinal: true,
                    nameof(JoinSyncSignal.BaselineApplied));
                break;
            case JoinSyncSignal.FinalBaselineApplied when CurrentPhase == JoinPhase.WaitingForFinalBaseline:
                QueueWorldReady(peer);
                break;
            case JoinSyncSignal.CatchUpApplied when CurrentPhase == JoinPhase.WaitingForCatchUpApplied:
                QueueCampaignEntry(peer);
                break;
        }
    }

    private void QueueBaseline(
        NetPeer peer,
        JoinPhase expected,
        bool isFinal,
        string context)
    {
        JoinPhase queued = isFinal ? JoinPhase.FinalBaselineQueued : JoinPhase.InitialBaselineQueued;
        JoinPhase waiting = isFinal ? JoinPhase.WaitingForFinalBaseline : JoinPhase.WaitingForInitialBaseline;
        if (!TrySetPhase(expected, queued)) return;

        GameThread.RunSafe(() =>
        {
            lock (joinGate)
            {
                if (!IsCurrent(queued)) return;

                if (totalBaselinesSent >= MaxBaselinesPerJoin)
                {
                    if (Interlocked.Exchange(ref baselineLimitLogged, 1) == 0)
                    {
                        Logger.Warning(
                            "Ignored campaign baseline request from peer {PeerId} after {Limit} baselines; " +
                            "the join watchdog will terminate the non-converging connection",
                            peer.Id,
                            MaxBaselinesPerJoin);
                    }
                    TrySetPhase(queued, waiting);
                    return;
                }

                AdvanceJoinProgress();
                totalBaselinesSent++;
                replayContinuation = isFinal
                    ? ReplayContinuation.FinalBaseline
                    : ReplayContinuation.InitialBaseline;
                if (!TrySetPhase(queued, JoinPhase.ReplayBatchQueued)) return;
                PumpReplay(peer);
            }
        }, context: context);
    }

    private void QueueWorldReady(NetPeer peer)
    {
        if (!TrySetPhase(JoinPhase.WaitingForFinalBaseline, JoinPhase.WorldReadyQueued)) return;
        AdvanceJoinProgress();
        GameThread.RunSafe(() =>
        {
            lock (joinGate)
            {
                if (!IsCurrent(JoinPhase.WorldReadyQueued)) return;

                replayContinuation = ReplayContinuation.WorldReady;
                if (!TrySetPhase(JoinPhase.WorldReadyQueued, JoinPhase.ReplayBatchQueued)) return;
                PumpReplay(peer);
            }
        }, context: nameof(JoinSyncSignal.FinalBaselineApplied));
    }

    private void QueueCampaignEntry(NetPeer peer)
    {
        if (!TrySetPhase(
                JoinPhase.WaitingForCatchUpApplied,
                JoinPhase.CatchUpAppliedQueued))
        {
            return;
        }
        AdvanceJoinProgress();
        GameThread.RunSafe(() =>
        {
            lock (joinGate)
            {
                if (!IsCurrent(JoinPhase.CatchUpAppliedQueued)) return;

                connectionMessageQueue.CompleteCatchUp(peer);

                if (playerManager.TryGetPlayer(peer, out var player))
                {
                    playerManager.MarkCampaignReady(player.ControllerId);
                }

                messageBroker.Publish(this, new PlayerCampaignSynchronized(peer));

                ConnectionLogic.EnterCampaign();
            }
        }, context: nameof(JoinSyncSignal.CatchUpApplied));
    }

    private void QueueNextReplayBatch(NetPeer peer, int replayBatchId)
    {
        lock (joinGate)
        {
            if ((!IsCurrent(JoinPhase.WaitingForReplayBatchApplied) &&
                 !IsCurrent(JoinPhase.ReplayBatchQueued)) ||
                replayBatches.Count == 0 || replayBatches.Peek().Id != replayBatchId)
            {
                return;
            }

            // Reliable ordered acknowledgements release only the oldest issued application credit.
            replayBatches.Dequeue();
            AdvanceJoinProgress();
            QueueReplayPump(peer);
        }
    }

    private void QueueReplayPump(NetPeer peer)
    {
        if (replayBatches.Count >= NetworkJoinLimits.MaxReplayInFlightBatches ||
            (replayBatches.Count > 0 &&
             (replayDrainBlocked || replayBatches.Peek().Bytes > NetworkJoinLimits.MaxReplayBatchBytes)) ||
            !TrySetPhase(JoinPhase.WaitingForReplayBatchApplied, JoinPhase.ReplayBatchQueued)) return;

        // The phase admits one queued pump, even when several acknowledgements arrive in one frame.
        GameThread.EnqueueSafe(() =>
        {
            lock (joinGate)
            {
                if (!IsCurrent(JoinPhase.ReplayBatchQueued)) return;
                PumpReplay(peer);
            }
        }, context: nameof(JoinSyncSignal.ReplayBatchApplied));
    }

    private void PumpReplay(NetPeer peer)
    {
        if (!IsCurrent(JoinPhase.ReplayBatchQueued)) return;

        // Include coalesced world mutations in this cut before deciding whether the bounded batch is
        // final. New mutations remain queued behind the application barrier.
        coalescer.Flush(network);

        JoinReplayBatchResult result;
        if (replayContinuation == ReplayContinuation.WorldReady && replayBatches.Count == 0)
        {
            result = connectionMessageQueue.OpenWithTailBatch(
                peer,
                new NetworkJoinSync(JoinSyncSignal.WorldReady),
                () => TrySetPhase(
                    JoinPhase.ReplayBatchQueued,
                    JoinPhase.WaitingForCatchUpApplied));
            if (result.Overflowed) return;
            if (!result.HasMore) return;
        }
        else
        {
            result = replayBatches.Count == 0
                ? connectionMessageQueue.FlushBatch(peer)
                : connectionMessageQueue.FlushBatch(peer, allowOversized: false);
            if (result.Overflowed) return;
            if (!result.HasMore && replayBatches.Count == 0)
            {
                CompleteReplayContinuation(peer);
                return;
            }
        }

        // Empty/oversized tails wait for all issued barriers before the next cut or baseline.
        replayDrainBlocked = !result.HasMore || result.PacketsSent == 0;
        if (!TrySetPhase(
                JoinPhase.ReplayBatchQueued,
                JoinPhase.WaitingForReplayBatchApplied))
        {
            return;
        }

        if (result.PacketsSent > 0)
        {
            int replayBatchId = ++nextReplayBatchId;
            replayBatches.Enqueue((replayBatchId, result.BytesSent));
            network.SendImmediate(
                peer,
                new NetworkJoinSync(JoinSyncSignal.ReplayBatchComplete, replayBatchId));
        }
        QueueReplayPump(peer);
    }

    private void CompleteReplayContinuation(NetPeer peer)
    {
        switch (replayContinuation)
        {
            case ReplayContinuation.InitialReplay:
                if (!TrySetPhase(
                        JoinPhase.ReplayBatchQueued,
                        JoinPhase.WaitingForReplayApplied))
                {
                    return;
                }
                network.SendImmediate(peer, new NetworkJoinSync(JoinSyncSignal.ReplayComplete));
                return;
            case ReplayContinuation.InitialBaseline:
                if (!TrySetPhase(
                        JoinPhase.ReplayBatchQueued,
                        JoinPhase.WaitingForInitialBaseline))
                {
                    return;
                }
                initialBaselinesSent++;
                campaignBaselineSender.Send(peer);
                campaignKingdomBaselineSender.Send(peer);
                return;
            case ReplayContinuation.FinalBaseline:
                if (!TrySetPhase(
                        JoinPhase.ReplayBatchQueued,
                        JoinPhase.WaitingForFinalBaseline))
                {
                    return;
                }
                connectionMessageQueue.EndFinalBaselineCoverage(peer);
                campaignBaselineSender.Send(peer);
                campaignKingdomBaselineSender.Send(peer);
                return;
        }
    }

    private void AdvanceJoinProgress() => Interlocked.Increment(ref joinProgressVersion);

    private JoinPhase CurrentPhase => (JoinPhase)Volatile.Read(ref phase);

    private static bool IsPendingJoinPhase(JoinPhase current) =>
        current >= JoinPhase.ReplayBatchQueued &&
        current != JoinPhase.CatchUpAppliedQueued &&
        current != JoinPhase.Aborted;

    private bool TrySetPhase(JoinPhase expected, JoinPhase next) =>
        ReferenceEquals(ConnectionLogic.State, this) &&
        Interlocked.CompareExchange(ref phase, (int)next, (int)expected) == (int)expected;

    private bool IsCurrent(JoinPhase expected) =>
        ReferenceEquals(ConnectionLogic.State, this) && CurrentPhase == expected;

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
