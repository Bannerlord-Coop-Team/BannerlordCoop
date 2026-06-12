using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Services.Connection.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;

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
        ITimeControlInterface timeControlInterface)
        : base(connectionLogic)
    {
        messageBroker.Publish(this, new PlayerLoading());

        GameLoopRunner.RunOnMainThread(() =>
        {
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);

            var saveResults = saveInterface.SaveCurrentGame();

            var networkEvent = new NetworkGameSaveDataReceived(
                saveResults.Data,
                saveResults.CampaignId,
                coopSessionProvider.CoopSession?.CraftingPlayerData);

            network.Send(ConnectionLogic.Peer, networkEvent);
        }, blocking: true);

        ConnectionLogic.Load();
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
