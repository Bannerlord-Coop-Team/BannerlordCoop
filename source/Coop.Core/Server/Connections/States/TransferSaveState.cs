using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.GameDebug.Messages;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection currently receiving the game state
/// through a save transfer
/// </summary>
public class TransferSaveState : ConnectionStateBase
{
    private IMessageBroker messageBroker;
    private INetwork network;

    public TransferSaveState(IConnectionLogic connectionLogic, IMessageBroker messageBroker, INetwork network)
        : base(connectionLogic)
    {
        this.network = network;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<GameSaveDataPackaged>(Handle_GameSaveDataPackaged);

        messageBroker.Publish(this, new SendInformationMessage("Préparation transfert de sauvegarde"));
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

        var data = payload.GameSaveData;
        if (data == null || data.Length == 0)
        {
            messageBroker.Publish(this, new SendInformationMessage("Sauvegarde indisponible côté serveur"));
            return;
        }

        var networkEvent = new NetworkGameSaveDataReceived(
            data,
            payload.CampaignID,
            null); // TODO manage controlled objects

        network.Send(peer, networkEvent);
        messageBroker.Publish(this, new SendInformationMessage("Sauvegarde envoyée au client"));

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
        ConnectionLogic.SetState<LoadingState>();
    }

    public override void TransferSave()
    {
    }
}
