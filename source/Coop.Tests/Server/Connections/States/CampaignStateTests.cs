using Autofac;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using LiteNetLib;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CampaignStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        public CampaignStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<IConnectionLogic>(new TypedParameter(typeof(NetPeer), playerPeer));

            differentPeer.SetId(playerPeer.Id + 1);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            connectionLogic.SetState<CampaignState>();

            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave();
            connectionLogic.Load();
            connectionLogic.EnterCampaign();

            Assert.IsType<CampaignState>(connectionLogic.State);
        }
    }
}
