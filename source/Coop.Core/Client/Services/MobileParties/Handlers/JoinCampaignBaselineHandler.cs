using Common;
using Common.Messaging;
using Common.Util;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.Time.Interfaces;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Applies an authoritative time and mobile-party baseline while a client is joining.
/// </summary>
public sealed class JoinCampaignBaselineHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;
    private readonly IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot;

    public JoinCampaignBaselineHandler(
        IMessageBroker messageBroker,
        IMapTimeTrackerInterface mapTimeTrackerInterface,
        IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot)
    {
        this.messageBroker = messageBroker;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;
        this.mobilePartyBehaviorSnapshot = mobilePartyBehaviorSnapshot;

        messageBroker.Subscribe<NetworkJoinCampaignBaseline>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkJoinCampaignBaseline>(Handle);
    }

    private void Handle(MessagePayload<NetworkJoinCampaignBaseline> payload)
    {
        var baseline = payload.What;
        GameThread.RunSafe(() =>
        {
            bool success = baseline.IsComplete &&
                mobilePartyBehaviorSnapshot.TryApplyJoinBaseline(
                    baseline.PartyStates,
                    () => mapTimeTrackerInterface.ApplyCampaignJoinBaseline(baseline.ServerTicks));

            messageBroker.Publish(this, new JoinCampaignBaselineApplied(success));
        }, context: nameof(JoinCampaignBaselineHandler));
    }
}
