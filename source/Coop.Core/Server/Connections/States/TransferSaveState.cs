using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Server.Services.Connection.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System.Linq;

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
        IPeerBroadcastGate broadcastGate,
        IPlayerManager playerManager)
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

            network.Send(ConnectionLogic.Peer, savePacket);

            // The save carries every player's hero but not the player registry, and the
            // per-player broadcast fires only once, at character creation — a peer that was
            // not yet receiving broadcasts at that moment never registers those players, so
            // their parties/heroes/clans are not marked player-controlled on that client.
            // Send the registry with the save; the client applies it once the campaign and
            // its object registry are ready.
            var players = playerManager.Players?.ToArray() ?? System.Array.Empty<Player>();
            network.Send(ConnectionLogic.Peer, new NetworkExistingPlayers(players));

            // Open world broadcasts to this peer only now: everything broadcast before this
            // point is inside the snapshot just sent. Ordered broadcasts enqueued after it
            // follow the save on the same reliable-ordered channel, where the client buffers
            // them until the campaign is ready; unordered packets (party behavior) may arrive
            // ahead of the save and rely on their lookup-guarded handlers instead.
            broadcastGate.Open(ConnectionLogic.Peer);
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
