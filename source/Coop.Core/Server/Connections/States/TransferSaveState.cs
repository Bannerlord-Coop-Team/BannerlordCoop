using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection currently receiving the game state
/// through a save transfer
/// </summary>
public class TransferSaveState : ConnectionStateBase
{
    private IMessageBroker messageBroker;
    private INetwork network;

    public TransferSaveState(IConnectionLogic connectionLogic)
        : base(connectionLogic)
    {
        network = ConnectionLogic.Network;
        messageBroker = ConnectionLogic.MessageBroker;

        messageBroker.Subscribe<GameSaveDataPackaged>(Handle_GameSaveDataPackaged);

        network.SendAll(new NetworkDisableTimeControls());
        // TODO will conflict with timemode changed event
        messageBroker.Publish(this, new PauseAndDisableGameTimeControls());
        messageBroker.Publish(this, new PackageGameSaveData());
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<GameSaveDataPackaged>(Handle_GameSaveDataPackaged);
    }

    
    internal void Handle_GameSaveDataPackaged(MessagePayload<GameSaveDataPackaged> obj)
    {
        var payload = obj.What;
        var peer = ConnectionLogic.Peer;
        var networkEvent = new NetworkGameSaveDataReceived(
            payload.GameSaveData,
            payload.CampaignID,
            null); // TODO manage controlled objects

        network.Send(peer, networkEvent);

        ConnectionLogic.Load();
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
        ConnectionLogic.State = new LoadingState(ConnectionLogic);
    }

    public override void TransferSave()
    {
    }
}
