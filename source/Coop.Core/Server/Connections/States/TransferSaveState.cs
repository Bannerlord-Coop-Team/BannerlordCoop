using Common;
using Common.Logging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Common.Network.Packets;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection currently receiving the game state
/// through a save transfer
/// </summary>
public class TransferSaveState : ConnectionStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<TransferSaveState>();

    public TransferSaveState(
        IConnectionLogic connectionLogic,
        INetwork network,
        ICoopSessionProvider coopSessionProvider,
        ISaveInterface saveInterface,
        ITimeControlInterface timeControlInterface,
        IConnectionMessageQueue connectionMessageQueue,
        ISendCoalescer coalescer,
        IAttachmentIdMapper attachmentIdMapper)
        : base(connectionLogic)
    {
        GameThread.Run(() =>
        {
            // Pause so the save snapshot is taken from a stationary world. This is local to the
            // save and runs before the connection has been assigned this state, so it precedes
            // the registry's loading lock; ConnectionCollection drives the broadcast loading pause once
            // the transition completes (see IsLoading below).
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);

            // Flush pending coalesced sends before the snapshot, while this peer is still Dropping: a deferred
            // delta would otherwise be both captured in the snapshot and replayed to this peer after BeginQueueing
            // (double-apply). Guarded so a throwing send can't strand this blocking GameThread.Run.
            try
            {
                coalescer.Flush(network);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to flush coalesced sends before the join save snapshot");
            }

            var saveResults = saveInterface.SaveCurrentGame();

            var savePacket = new GameSaveDataPacket(
                SaveDataCompression.Compress(saveResults.Data),
                saveResults.CampaignId,
                coopSessionProvider.CoopSession?.CraftingPlayerData,
                coopSessionProvider.CoopSession?.WorkshopPlayerData,
                coopSessionProvider.CoopSession?.CaravansPlayerData,
                coopSessionProvider.CoopSession?.AlleyPlayerData,
                coopSessionProvider.CoopSession?.InteractionsPlayerData,
                attachmentIdMapper.BuildServerMap());

            // Disconnect peer on failure
            if (!saveResults.Success)
            {
                connectionLogic.Peer.Disconnect();
                return;
            }

            // Start holding this peer's broadcasts now that the snapshot has been taken. The whole save
            // runs in a blocking GameThread.Run call issued from the network thread, so the poller is
            // parked for its duration and cannot broadcast a received delta that races the snapshot;
            // taking the cut right after the snapshot cleanly separates "in the save" (dropped while
            // Dropping) from "after the save" (queued for replay).
            connectionMessageQueue.BeginQueueing(ConnectionLogic.Peer);

            network.SendImmediate(ConnectionLogic.Peer, savePacket);
        }, blocking: true);
    }

    public override bool IsLoading => true;

    public override void Dispose()
    {
    }

    public override void CreateCharacter()
    {
    }

    public override void EnterCampaign()
    {
    }

    public override void EnterMission()
    {
    }

    public override void Load()
    {
        ConnectionLogic.SetState<LoadingState>();
    }

    public override void TransferSave()
    {
    }
}
