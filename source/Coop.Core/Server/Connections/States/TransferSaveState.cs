using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Enum;
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
    private ICoopSessionProvider coopSessionProvider;

    public TransferSaveState(IConnectionLogic connectionLogic, IMessageBroker messageBroker, INetwork network, ICoopSessionProvider coopSessionProvider)
        : base(connectionLogic)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.coopSessionProvider = coopSessionProvider;

        messageBroker.Subscribe<GameSaveDataPackaged>(Handle_GameSaveDataPackaged);

        messageBroker.Publish(this, new SetTimeControlMode(TimeControlEnum.Pause));
        network.SendAll(new NetworkChangeTimeControlMode(TimeControlEnum.Pause));

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
            null, // TODO manage controlled objects
            coopSessionProvider.CoopSession.CraftingPlayerData);

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
        ConnectionLogic.SetState<LoadingState>();
    }

    public override void TransferSave()
    {
    }
}
