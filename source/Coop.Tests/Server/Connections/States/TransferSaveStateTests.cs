using Autofac;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Registry;
using LiteNetLib;
using Moq;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class TransferSaveStateTests
    {
        // A server test container that additionally mocks the send coalescer and the connection message
        // queue, so a test can record the order in which TransferSaveState flushes pending sends, takes
        // the save snapshot, and starts queueing the joining peer's broadcasts.
        private sealed class TransferSaveServerComponent : TestComponentBase
        {
            public TransferSaveServerComponent(ITestOutputHelper output) : base(output)
            {
                var builder = new ContainerBuilder();
                builder.RegisterModule<ServerModule>();
                builder.RegisterModule<RegistryModule>();

                RegisterMock<ICoopServer>(builder);
                RegisterMock<ISendCoalescer>(builder);
                RegisterMock<IConnectionMessageQueue>(builder);

                Container = BuildContainer(builder);
            }
        }

        private readonly TransferSaveServerComponent component;
        private readonly IConnectionLogic connectionLogic;

        public TransferSaveStateTests(ITestOutputHelper output)
        {
            component = new TransferSaveServerComponent(output);

            var network = component.Container.Resolve<TestNetwork>();
            var playerPeer = network.CreatePeer();
            connectionLogic = component.Container.Resolve<ConnectionLogic>(
                new TypedParameter(typeof(NetPeer), playerPeer));
        }

        [Fact]
        public void EnteringState_FlushesCoalescer_BeforeSnapshotAndQueueing()
        {
            // Arrange — record the order of the flush and the queueing handoff. The save itself uses the
            // harness's default ISaveInterface mock (returns success), so the block reaches BeginQueueing.
            var order = new List<string>();

            component.Container.Resolve<Mock<ISendCoalescer>>()
                .Setup(c => c.Flush(It.IsAny<INetwork>()))
                .Callback(() => order.Add("Flush"));

            component.Container.Resolve<Mock<IConnectionMessageQueue>>()
                .Setup(q => q.BeginQueueing(It.IsAny<NetPeer>()))
                .Callback(() => order.Add("BeginQueueing"));

            // Act — entering TransferSaveState runs its blocking save block on the pumped game thread.
            connectionLogic.SetState<TransferSaveState>();

            // Assert — the coalescer is flushed while the peer is still Dropping (before BeginQueueing), so a
            // delta already captured in the save is not also queued and replayed to the joining peer (the
            // double-apply this fix prevents). Placement relative to the snapshot itself is not load-bearing.
            Assert.Contains("Flush", order);
            Assert.True(order.IndexOf("Flush") < order.IndexOf("BeginQueueing"),
                "coalescer must be flushed while the peer is still Dropping (before BeginQueueing)");
        }
    }
}
