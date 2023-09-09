using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection being in the campaign
/// </summary>
public class CampaignState : ConnectionStateBase
{
    private readonly IMessageBroker messageBroker;

    public CampaignState(IConnectionLogic connectionLogic, IMessageBroker messageBroker) : base(connectionLogic)
    {
        messageBroker.Subscribe<NetworkPlayerMissionEntered>(PlayerMissionEnteredHandler);
        this.messageBroker = messageBroker;
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerMissionEntered>(PlayerMissionEnteredHandler);
    }

    internal void PlayerMissionEnteredHandler(MessagePayload<NetworkPlayerMissionEntered> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId == ConnectionLogic.Peer)
        {
            ConnectionLogic.EnterMission();
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
    }

    public override void EnterMission()
    {
        ConnectionLogic.SetState<MissionState>();
    }
}
