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

            // Disconnect peer on failure — before touching Data, which a failed snapshot may leave null.
            if (!saveResults.Success)
            {
                Logger.Error("Join save snapshot failed for peer {PeerId}; disconnecting", connectionLogic.Peer.Id);
                connectionLogic.Peer.Disconnect();
                return;
            }

            var compressedSave = SaveDataCompression.Compress(saveResults.Data);

            // Forensic fingerprint for join decode failures: the client logs the same fingerprint of what
            // it RECEIVED (GameSaveDataPacketHandler) — compare the two lines when a join fails.
            Logger.Information("Join transfer save for peer {PeerId}: raw {RawBytes} bytes → compressed {Fingerprint} (campaign {CampaignId})",
                connectionLogic.Peer.Id, saveResults.Data?.Length ?? 0,
                SaveDataCompression.Describe(compressedSave), saveResults.CampaignId);

            var savePacket = new GameSaveDataPacket(
                compressedSave,
                saveResults.CampaignId,
                coopSessionProvider.CoopSession?.CraftingPlayerData,
                coopSessionProvider.CoopSession?.WorkshopPlayerData,
                coopSessionProvider.CoopSession?.CaravansPlayerData,
                coopSessionProvider.CoopSession?.AlleyPlayerData,
                coopSessionProvider.CoopSession?.InteractionsPlayerData,
                attachmentIdMapper.BuildServerMap());

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
