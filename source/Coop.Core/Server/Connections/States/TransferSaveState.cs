using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection currently receiving the game state
/// through a save transfer
/// </summary>
public class TransferSaveState : ConnectionStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly ITimeControlInterface timeControlInterface;

    public TransferSaveState(
        IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        ICoopSessionProvider coopSessionProvider,
        ITimeControlInterface timeControlInterface)
        : base(connectionLogic)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.coopSessionProvider = coopSessionProvider;
        this.timeControlInterface = timeControlInterface;
        messageBroker.Subscribe<GameSaveDataPackaged>(Handle_GameSaveDataPackaged);

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);

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
            coopSessionProvider.CoopSession?.CraftingPlayerData);

        // SaveHandler responds synchronously, so this runs in the same main-thread block as
        // the snapshot: the save is enqueued before any world change taken after it, which is
        // what lets the joining client safely discard everything received before the save.
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
