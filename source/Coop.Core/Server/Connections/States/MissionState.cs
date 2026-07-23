using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection being in a mission
/// </summary>
public class MissionState : ConnectionStateBase
{
    private readonly IMessageBroker messageBroker;

    public MissionState(IConnectionLogic connectionLogic, IMessageBroker messageBroker)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerTransitionsCampaignHandler);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerTransitionsCampaignHandler);
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
        ConnectionLogic.SetState<CampaignState>();
    }

    public override void EnterMission()
    {
    }
}
