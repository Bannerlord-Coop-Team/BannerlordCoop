using System.Linq;
using Coop.Game.Persistence;
using Coop.Game.Persistence.World;
using Coop.Multiplayer.Network;
using Moq;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests
{
    public class Rail_Test
    {
        private readonly RailClient m_Client;
        private readonly RailServer m_Server;

        private readonly Mock<RailNetPeerWrapper> m_PeerClientSide;
        private readonly Mock<RailNetPeerWrapper> m_PeerServerSide;

        private readonly InMemoryConnection m_ConClientSide = new InMemoryConnection();
        private readonly InMemoryConnection m_ConServerSide = new InMemoryConnection();

        private readonly TestEnvironment m_EnvironmentClient = new TestEnvironment();
        private readonly TestEnvironment m_EnvironmentServer = new TestEnvironment();

        public Rail_Test()
        {
            m_Client = new RailClient(Registry.Get(Component.Client, m_EnvironmentClient));
            m_Server = new RailServer(Registry.Get(Component.Server, m_EnvironmentServer));


            m_PeerClientSide = new Mock<RailNetPeerWrapper>(m_ConClientSide)
            {
                CallBase = true
            };
            m_PeerServerSide = new Mock<RailNetPeerWrapper>(m_ConServerSide)
            {
                CallBase = true
            };

            m_ConClientSide.OnSend += m_PeerServerSide.Object.Receive;
            m_ConServerSide.OnSend += m_PeerClientSide.Object.Receive;
        }

        [Fact]
        void ClientServerCommunication()
        {
            // Initialization
            CampaignTimeControlMode expectedTimeControl = CampaignTimeControlMode.StoppablePlay;
            m_EnvironmentClient.TimeControlMode = CampaignTimeControlMode.FastForwardStop;
            m_EnvironmentServer.TimeControlMode = expectedTimeControl;
            RailClientRoom clientRoom = m_Client.StartRoom();
            RailServerRoom serverRoom = m_Server.StartRoom();
            WorldEntityServer entityServerSide = serverRoom.AddNewEntity<WorldEntityServer>();
            m_Server.AddClient(m_PeerServerSide.Object, "");
            m_Client.SetPeer(m_PeerClientSide.Object);
            Assert.Empty(clientRoom.Entities);
            Assert.Single(serverRoom.Entities);

            // Sync world entity from server to client
            for (int i = 0; i < RailConfig.SERVER_SEND_RATE + RailConfig.CLIENT_SEND_RATE + 1; ++i)
            {
                m_ConClientSide.ExecuteSends();
                m_Server.Update();
                m_ConServerSide.ExecuteSends();
                m_Client.Update();
            }

            // The client has received the world entity.
            Assert.Single(clientRoom.Entities);
            Assert.Single(serverRoom.Entities);

            // Clients representation of the entity is identical to the server
            RailEntityBase entityProxy = clientRoom.Entities.First();
            Assert.IsType<WorldEntityClient>(entityProxy);
            WorldEntityClient entityClientSide = entityProxy as WorldEntityClient;
            Assert.NotNull(entityClientSide);
            Assert.Equal(entityServerSide.Id, entityProxy.Id);
            Assert.Equal(expectedTimeControl, entityServerSide.State.TimeControlMode);
            Assert.Equal(expectedTimeControl, entityClientSide.State.TimeControlMode);
            Assert.Equal(entityClientSide.State.TimeControlMode, m_EnvironmentClient.TimeControlMode);
            Assert.Equal(entityServerSide.State.TimeControlMode, m_EnvironmentServer.TimeControlMode);

            // Change the entity on server side and sync to the client
            expectedTimeControl = CampaignTimeControlMode.Stop;
            m_EnvironmentServer.TimeControlMode = expectedTimeControl;
            Assert.NotEqual(m_EnvironmentServer.TimeControlMode, m_EnvironmentClient.TimeControlMode); // not directly linked!

            // Let the server detect the change and send the packet
            bool bWasSendTick = false;
            while (!bWasSendTick)
            {
                m_Server.Update();
                bWasSendTick = serverRoom.Tick.IsSendTick(RailConfig.SERVER_SEND_RATE);
            }

            // Let the client receive & process the packet. We need to bring the client up to the same tick as the server to see the result.
            while (clientRoom.Tick <= serverRoom.Tick)
            {
                m_ConServerSide.ExecuteSends();
                m_Client.Update();
            }

            Assert.Equal(expectedTimeControl, m_EnvironmentServer.TimeControlMode);
            Assert.Equal(expectedTimeControl, m_EnvironmentClient.TimeControlMode);

            // Request a time change on the client
            expectedTimeControl = CampaignTimeControlMode.StoppableFastForward;
            m_EnvironmentClient.RequestedTimeControlMode = expectedTimeControl;

            // Let the client detect the request & send an event to the server
            bWasSendTick = false;
            while (!bWasSendTick)
            {
                m_Client.Update();
                bWasSendTick = serverRoom.Tick.IsSendTick(RailConfig.SERVER_SEND_RATE);
            }

            // Let the server receive & process it
            m_ConClientSide.ExecuteSends();
            Assert.Equal(expectedTimeControl, m_EnvironmentServer.TimeControlMode);

            // And sync back to client
            bWasSendTick = false;
            while (!bWasSendTick)
            {
                m_Server.Update();
                bWasSendTick = serverRoom.Tick.IsSendTick(RailConfig.SERVER_SEND_RATE);
            }
            while (clientRoom.Tick <= serverRoom.Tick)
            {
                m_ConServerSide.ExecuteSends();
                m_Client.Update();
            }
            Assert.Equal(expectedTimeControl, m_EnvironmentClient.TimeControlMode);
        }
    }
}
