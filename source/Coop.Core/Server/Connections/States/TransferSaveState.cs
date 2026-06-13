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

            // Start holding this peer's broadcasts at the save boundary, on the main thread. This is the
            // approximate cut between "in the save" (dropped while Dropping) and "after the save" (queued
            // for replay) — approximate because the save below is not atomic w.r.t. the network poller;
            // see ConnectionMessageQueue for the residual duplicate/loss windows.
            connectionMessageQueue.BeginQueueing(ConnectionLogic.Peer);

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
                
            network.Send(ConnectionLogic.Peer, savePacket);
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
