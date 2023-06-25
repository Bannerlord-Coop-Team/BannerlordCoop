using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection being in a mission
/// </summary>
public class MissionState : ConnectionStateBase
{
    public MissionState(IConnectionLogic connectionLogic)
        : base(connectionLogic)
    {
        ConnectionLogic.MessageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerTransitionsCampaignHandler);
    }

    public override void Dispose()
    {
        ConnectionLogic.MessageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerTransitionsCampaignHandler);
    }

    internal void PlayerTransitionsCampaignHandler(MessagePayload<NetworkPlayerCampaignEntered> obj)
    {
        var playerId = (NetPeer)obj.Who;
        if (playerId != ConnectionLogic.Peer) return;

        ConnectionLogic.EnterCampaign();
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
