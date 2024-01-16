using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.Time
{
    public class TimeHandlerTests
    {
        private ServerTestComponent serverTestComponent;

        public TimeHandlerTests(ITestOutputHelper output)
        {
            serverTestComponent = new ServerTestComponent(output);
        }

        [Fact]
        public void NetworkRequestTimeSpeedChange_Publishes_SetTimeControlMode()
        {
            // Arrange
            var handler = serverTestComponent.Container.Resolve<TimeHandler>();
            var broker = serverTestComponent.TestMessageBroker;
            var payload = new NetworkRequestTimeSpeedChange(TimeControlEnum.Pause);
            var message = new MessagePayload<NetworkRequestTimeSpeedChange>(null, payload);

            // Act
            handler.Handle_NetworkRequestTimeSpeedChange(message);

            // Assert
            Assert.Single(broker.Messages);
            Assert.IsType<SetTimeControlMode>(broker.Messages.First());
            var setTimeControlMode = (SetTimeControlMode)broker.Messages.First();
            Assert.Equal(payload.NewControlMode, setTimeControlMode.NewTimeMode);
        }

        [Fact]
        public void TimeSpeedChanged_Publishes_NetworkTimeSpeedChanged()
        {
            // Arrange
            var handler = serverTestComponent.Container.Resolve<TimeHandler>();
            var network = serverTestComponent.Container.Resolve<TestNetwork>();
            var message = new MessagePayload<AttemptedTimeSpeedChanged>(null, new AttemptedTimeSpeedChanged(TimeControlEnum.Play_1x));

            network.CreatePeer();

            // Act
            handler.Handle_TimeSpeedChanged(message);

            // Assert
            Assert.NotEmpty(network.Peers);
            foreach(var peer in network.Peers)
            {
                var speedChangedMessage = Assert.Single(network.GetPeerMessages(peer));
                Assert.IsType<NetworkChangeTimeControlMode>(speedChangedMessage);
            }
        }
    }
}
