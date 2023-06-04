using Common.Messaging;
using Coop.Core.Client.Services.MobileParties.Handlers;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Client.Services.MobileParties
{
    public class MobilePartyBehaviorHandlerTest
    {
        private readonly MockMessageBroker broker;
        private readonly MockNetwork network;
        private readonly MobilePartyBehaviorHandler handler;

        public MobilePartyBehaviorHandlerTest()
        {
            // Arange
            broker = new MockMessageBroker();
            network = new MockNetwork();
            handler = new MobilePartyBehaviorHandler(broker, network);
        }

        [Fact]
        public void Constructor_SubscribesToEvents()
        {
            // Assert
            Assert.NotEmpty(broker.Subscriptions);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Act
            handler.Dispose();

            // Assert
            Assert.Empty(broker.Subscriptions);
        }

        [Fact]
        public void ControlledPartyAiBehaviorUpdated_Publishes_NetworkRequestMobilePartyAiBehavior() 
        {
            // Arrange
            var data = new PartyBehaviorUpdateData("party", AiBehavior.GoToPoint, false, string.Empty, new Vec2(1, 2));
            var payload = new ControlledPartyBehaviorUpdated(data);
            var message = new MessagePayload<ControlledPartyBehaviorUpdated>(null, payload);

            var peer = network.CreatePeer();

            // Act
            handler.Handle(message);

            // Assert
            var sentMessages = network.GetPeerMessages(peer);
            Assert.Single(sentMessages);
            Assert.IsType<NetworkRequestMobilePartyBehavior>(sentMessages.First());

            var networkRequestTimeSpeedChange = (NetworkRequestMobilePartyBehavior)sentMessages.First();
            Assert.Equal(message.What.BehaviorUpdateData, networkRequestTimeSpeedChange.BehaviorUpdateData);
        }

        [Fact]
        public void NetworkUpdatePartyAiBehavior_Publishes_UpdatePartyAiBehavior()
        {
            // Arrange
            var data = new PartyBehaviorUpdateData("party", AiBehavior.GoToPoint, false, string.Empty, new Vec2(1, 2));
            var payload = new NetworkUpdatePartyBehavior(data);
            var message = new MessagePayload<NetworkUpdatePartyBehavior>(null, payload);

            // Act
            handler.Handle(message);

            // Assert
            var updateBehaviorMessage = Assert.Single(broker.PublishedMessages);
            var updatePartyAiBehavior = Assert.IsType<UpdatePartyBehavior>(updateBehaviorMessage);
            Assert.Equal(payload.BehaviorUpdateData, updatePartyAiBehavior.BehaviorUpdateData);
        }
    }
}
