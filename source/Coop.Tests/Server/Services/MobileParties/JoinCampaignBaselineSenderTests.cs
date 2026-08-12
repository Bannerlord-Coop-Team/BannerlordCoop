using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Server.Services.MobileParties;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.Time.Interfaces;
using LiteNetLib;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Server.Services.MobileParties;

/// <summary>
/// Serializes tests that replace the global current campaign.
/// </summary>
[CollectionDefinition(nameof(CampaignCurrentCollection), DisableParallelization = true)]
public sealed class CampaignCurrentCollection
{
}

/// <summary>
/// Tests authoritative mobile-party join-baseline capture.
/// </summary>
[Collection(nameof(CampaignCurrentCollection))]
public class JoinCampaignBaselineSenderTests
{
    [Fact]
    public void Send_InactiveParty_CapturesOnlyActiveParty()
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var activeParty = ObjectHelper.SkipConstructor<MobileParty>();
            activeParty.IsActive = true;
            var inactiveParty = ObjectHelper.SkipConstructor<MobileParty>();
            inactiveParty.IsActive = false;
            var campaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            campaignObjectManager._mobileParties.Add(activeParty);
            campaignObjectManager._mobileParties.Add(inactiveParty);
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            campaign.CampaignObjectManager = campaignObjectManager;
            Campaign.Current = campaign;

            var network = new Mock<INetwork>();
            var mapTimeTracker = new Mock<IMapTimeTrackerInterface>();
            long serverTicks = 123L;
            mapTimeTracker
                .Setup(tracker => tracker.TryGetCurrentTicks(out serverTicks))
                .Returns(true);
            var snapshot = new Mock<IMobilePartyBehaviorSnapshot>();
            var state = new MobilePartyJoinState();
            string failure = null!;
            snapshot
                .Setup(service => service.TryCreateJoinState(
                    activeParty,
                    It.Is<ISet<MobileParty>>(parties =>
                        parties.Count == 1 && parties.Contains(activeParty)),
                    It.IsAny<ISet<Settlement>>(),
                    out state,
                    out failure))
                .Returns(true);
            var timeControl = new Mock<ITimeControlInterface>();
            timeControl.Setup(service => service.GetTimeControl()).Returns(TimeControlEnum.Pause);
            NetworkJoinCampaignBaseline sent = default;
            network
                .Setup(service => service.SendImmediate(
                    It.IsAny<NetPeer>(),
                    It.IsAny<IMessage>()))
                .Callback<NetPeer, IMessage>((_, message) =>
                    sent = Assert.IsType<NetworkJoinCampaignBaseline>(message));
            var sender = new JoinCampaignBaselineSender(
                network.Object,
                mapTimeTracker.Object,
                snapshot.Object,
                timeControl.Object);

            sender.Send(null!);

            Assert.True(sent.IsComplete);
            Assert.Single(sent.PartyStates!);
            snapshot.Verify(service => service.TryCreateJoinState(
                inactiveParty,
                It.IsAny<ISet<MobileParty>>(),
                It.IsAny<ISet<Settlement>>(),
                out It.Ref<MobilePartyJoinState>.IsAny,
                out It.Ref<string>.IsAny), Times.Never);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }
}
