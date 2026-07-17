using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection loading
/// </summary>
public class LoadingState : ConnectionStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IJoinMobilePartyPositionSnapshotSender positionSnapshotSender;
    private bool campaignEntryQueued;
    private bool catchUpAppliedQueued;

    public LoadingState(
        IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IJoinMobilePartyPositionSnapshotSender positionSnapshotSender)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.positionSnapshotSender = positionSnapshotSender;

        messageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Subscribe<NetworkJoinCatchUpApplied>(JoinCatchUpAppliedHandler);
    }

    public override bool IsLoading => true;

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Unsubscribe<NetworkJoinCatchUpApplied>(JoinCatchUpAppliedHandler);
    }

    internal void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId != ConnectionLogic.Peer || campaignEntryQueued) return;

        campaignEntryQueued = true;
        GameThread.RunSafe(() =>
        {
            // Publish on the game thread so handlers that marshal their join snapshots there run inline.
            messageBroker.Publish(this, new PlayerCampaignEntered(playerId));
            positionSnapshotSender.Send(playerId);
            network.SendImmediate(playerId, new NetworkJoinCatchUpComplete());
        }, context: nameof(PlayerCampaignEnteredHandler));
    }

    internal void JoinCatchUpAppliedHandler(MessagePayload<NetworkJoinCatchUpApplied> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId != ConnectionLogic.Peer || !campaignEntryQueued || catchUpAppliedQueued) return;

        catchUpAppliedQueued = true;
        GameThread.RunSafe(ConnectionLogic.EnterCampaign, context: nameof(JoinCatchUpAppliedHandler));
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
