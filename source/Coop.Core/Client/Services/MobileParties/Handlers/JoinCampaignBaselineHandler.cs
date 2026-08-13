using Common;
using Common.Messaging;
using Common.Util;
using Coop.Core.Client.Messages;
#if DEBUG
using Coop.Core.Common.Commands;
#endif
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Heroes.Interaces;
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
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IPlayerPartyTroopXpBaselineApplier troopXpBaselineApplier;

    public JoinCampaignBaselineHandler(
        IMessageBroker messageBroker,
        IMapTimeTrackerInterface mapTimeTrackerInterface,
        IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot,
        ITimeControlInterface timeControlInterface,
        IPlayerPartyTroopXpBaselineApplier troopXpBaselineApplier)
    {
        this.messageBroker = messageBroker;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;
        this.mobilePartyBehaviorSnapshot = mobilePartyBehaviorSnapshot;
        this.timeControlInterface = timeControlInterface;
        this.troopXpBaselineApplier = troopXpBaselineApplier;

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
#if DEBUG
            if (baseline.IsComplete)
            {
                JoinDebugCommands.ForceArmedInactivePartyDeficit();
            }
#endif
            bool success = baseline.IsComplete &&
                mobilePartyBehaviorSnapshot.TryApplyJoinBaseline(
                    baseline.PartyStates,
                    () =>
                    {
                        timeControlInterface.ClientSetTimeControl(baseline.TimeControlMode);
                        mapTimeTrackerInterface.ApplyCampaignJoinBaseline(baseline.ServerTicks);
                    }) &&
                troopXpBaselineApplier.TryApply(baseline.TroopXpBaselines);

            messageBroker.Publish(this, new JoinCampaignBaselineApplied(success));
        }, context: nameof(JoinCampaignBaselineHandler));
    }
}
