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
    private readonly IJoinCampaignBaselineSender campaignBaselineSender;
    private bool campaignEntryQueued;
    private volatile bool replayMarkerSent;
    private bool baselineQueued;
    private volatile bool baselineSent;
    private volatile bool baselineRefreshQueued;
    private volatile bool baselineRefreshed;
    private bool catchUpAppliedQueued;

    public LoadingState(
        IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IJoinCampaignBaselineSender campaignBaselineSender)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.campaignBaselineSender = campaignBaselineSender;

        messageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Subscribe<NetworkJoinReplayApplied>(JoinReplayAppliedHandler);
        messageBroker.Subscribe<NetworkJoinCampaignBaselineRequested>(JoinCampaignBaselineRequestedHandler);
        messageBroker.Subscribe<NetworkJoinCatchUpApplied>(JoinCatchUpAppliedHandler);
    }

    public override bool IsLoading => true;

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        messageBroker.Unsubscribe<NetworkJoinReplayApplied>(JoinReplayAppliedHandler);
        messageBroker.Unsubscribe<NetworkJoinCampaignBaselineRequested>(JoinCampaignBaselineRequestedHandler);
        messageBroker.Unsubscribe<NetworkJoinCatchUpApplied>(JoinCatchUpAppliedHandler);
    }

    internal void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId != ConnectionLogic.Peer || campaignEntryQueued) return;

        campaignEntryQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(ConnectionLogic.State, this) == false) return;

            // Flushing first puts the marker at the tail of every held reliable world update.
            messageBroker.Publish(this, new PlayerCampaignEntered(playerId));
            replayMarkerSent = true;
            network.SendImmediate(playerId, new NetworkJoinReplayComplete());
        }, context: nameof(PlayerCampaignEnteredHandler));
    }

    internal void JoinReplayAppliedHandler(MessagePayload<NetworkJoinReplayApplied> obj)
    {
        var playerId = (NetPeer)obj.Who;
        if (playerId != ConnectionLogic.Peer || !replayMarkerSent || baselineQueued) return;

        baselineQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(ConnectionLogic.State, this) == false) return;

            baselineSent = true;
            campaignBaselineSender.Send(playerId);
        }, context: nameof(JoinReplayAppliedHandler));
    }

    internal void JoinCampaignBaselineRequestedHandler(
        MessagePayload<NetworkJoinCampaignBaselineRequested> obj)
    {
        var playerId = (NetPeer)obj.Who;
        if (playerId != ConnectionLogic.Peer ||
            !baselineSent ||
            baselineRefreshQueued ||
            catchUpAppliedQueued)
        {
            return;
        }

        baselineRefreshQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(ConnectionLogic.State, this) == false) return;

            baselineRefreshed = true;
            baselineRefreshQueued = false;
            campaignBaselineSender.Send(playerId);
        }, context: nameof(JoinCampaignBaselineRequestedHandler));
    }

    internal void JoinCatchUpAppliedHandler(MessagePayload<NetworkJoinCatchUpApplied> obj)
    {
        var playerId = (NetPeer)obj.Who;

        if (playerId != ConnectionLogic.Peer ||
            !baselineSent ||
            baselineRefreshQueued ||
            !baselineRefreshed ||
            catchUpAppliedQueued)
        {
            return;
        }

        catchUpAppliedQueued = true;
        GameThread.RunSafe(() =>
        {
            if (ReferenceEquals(ConnectionLogic.State, this) == false) return;

            ConnectionLogic.EnterCampaign();
        }, context: nameof(JoinCatchUpAppliedHandler));
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
