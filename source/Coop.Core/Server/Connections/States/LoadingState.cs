using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection loading
/// </summary>
public class LoadingState : ConnectionStateBase
{
    public LoadingState(IConnectionLogic connectionLogic)
        : base(connectionLogic)
    {
        ConnectionLogic.MessageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
    }

    public override void Dispose()
    {
        ConnectionLogic.MessageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
    }

    internal void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId == ConnectionLogic.Peer)
        {
            ConnectionLogic.EnterCampaign();
            ConnectionLogic.MessageBroker.Publish(this, new PlayerCampaignEntered());
        }
    }

    public override void CreateCharacter()
    {
    }

    public override void TransferSave()
    {
    }

    public override void Load()
    {
    }

    public override void EnterCampaign()
    {
        ConnectionLogic.State = new CampaignState(ConnectionLogic);
    }

    public override void EnterMission()
    {
    }
}
