using Common;
using Common.Messaging;
using Common.Util;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Time.Interfaces;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Applies an authoritative time and party-position baseline while a client is joining.
/// </summary>
public sealed class JoinCampaignBaselineHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;

    public JoinCampaignBaselineHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IMapTimeTrackerInterface mapTimeTrackerInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;

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
            mapTimeTrackerInterface.ApplyCampaignJoinBaseline(baseline.ServerTicks);

            using (new AllowedThread())
            {
                foreach (var position in baseline.Positions)
                {
                    if (!objectManager.TryGetObjectWithLogging(position.MobilePartyId, out MobileParty party))
                        continue;

                    party.Position = position.ToCampaignVec2();
                }
            }

            messageBroker.Publish(this, new JoinCampaignBaselineApplied());
        }, context: nameof(JoinCampaignBaselineHandler));
    }
}
