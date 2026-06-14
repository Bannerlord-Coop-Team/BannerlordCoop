using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Server.Services.Connection.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Interfaces;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection currently receiving the game state
/// through a save transfer
/// </summary>
public class TransferSaveState : ConnectionStateBase
{
    public TransferSaveState(
        IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        ICoopSessionProvider coopSessionProvider,
        ISaveInterface saveInterface,
        ITimeControlInterface timeControlInterface,
        IConnectionMessageQueue connectionMessageQueue)
        : base(connectionLogic)
    {
        messageBroker.Publish(this, new PlayerLoading());

        GameLoopRunner.RunOnMainThread(() =>
        {
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);

            var saveResults = saveInterface.SaveCurrentGame();

            var savePacket = new GameSaveDataPacket(
                saveResults.Data,
                saveResults.CampaignId,
                coopSessionProvider.CoopSession?.CraftingPlayerData);

            // Disconnect peer on failure
            if (!saveResults.Success)
            {
                connectionLogic.Peer.Disconnect();
                return;
            }

            // Start holding this peer's broadcasts now that the snapshot has been taken. The whole save
            // runs in a blocking RunOnMainThread call issued from the network thread, so the poller is
            // parked for its duration and cannot broadcast a received delta that races the snapshot;
            // taking the cut right after the snapshot cleanly separates "in the save" (dropped while
            // Dropping) from "after the save" (queued for replay).
            connectionMessageQueue.BeginQueueing(ConnectionLogic.Peer);

            network.SendImmediate(ConnectionLogic.Peer, savePacket);
        }, blocking: true);
    }

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
