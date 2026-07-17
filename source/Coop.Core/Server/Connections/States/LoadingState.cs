using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection loading
/// </summary>
public class LoadingState : ConnectionStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private bool campaignEntryQueued;

    public LoadingState(IConnectionLogic connectionLogic, IMessageBroker messageBroker, INetwork network)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
    }

    public override bool IsLoading => true;

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
    }

    internal void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId != ConnectionLogic.Peer || campaignEntryQueued) return;

        campaignEntryQueued = true;
        GameThread.RunSafe(() =>
        {
            // Publish on the game thread so handlers that marshal their join snapshots there run
            // inline. The ordered marker is sent last, after the held stream and every join snapshot.
            ConnectionLogic.EnterCampaign();
            messageBroker.Publish(this, new PlayerCampaignEntered(playerId));
            network.SendImmediate(playerId, new NetworkJoinCatchUpComplete());
        }, context: nameof(PlayerCampaignEnteredHandler));
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
