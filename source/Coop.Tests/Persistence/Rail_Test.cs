using System.Linq;
using System.Reflection;
using Coop.Mod.Persistence;
using Coop.Multiplayer.Network;
using Moq;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests
{
    public class Rail_Test
    {
        private class SomeState : RailState
        {
            [Mutable] public CampaignTimeControlMode Mode { get; set; }
        }
        public Rail_Test()
        {
            RailSynchronizedFactory.Detect(Assembly.GetAssembly(typeof(RailBitBufferExtensions)));

            RailRegistry registryClient = new RailRegistry(Component.Client);
            registryClient.AddEntityType<RailEntityClient<SomeState>, SomeState>();

            RailRegistry registryServer = new RailRegistry(Component.Server);
            registryServer.AddEntityType<RailEntityServer<SomeState>, SomeState>();


            m_Client = new RailClient(registryClient);
            m_Server = new RailServer(registryServer);

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

        private readonly RailClient m_Client;
        private readonly RailServer m_Server;

        private readonly Mock<RailNetPeerWrapper> m_PeerClientSide;
        private readonly Mock<RailNetPeerWrapper> m_PeerServerSide;

        private readonly InMemoryConnection m_ConClientSide = new InMemoryConnection();
        private readonly InMemoryConnection m_ConServerSide = new InMemoryConnection();

        [Fact]
        private void ClientServerCommunication()
        {
            // Initialization
            CampaignTimeControlMode expectedTimeControl = CampaignTimeControlMode.StoppablePlay;
            RailClientRoom clientRoom = m_Client.StartRoom();
            RailServerRoom serverRoom = m_Server.StartRoom();
            RailEntityServer<SomeState> entityServerSide = serverRoom.AddNewEntity<RailEntityServer<SomeState>>();
            entityServerSide.State.Mode = expectedTimeControl;
            m_Server.AddClient(m_PeerServerSide.Object, "");
            m_Client.SetPeer(m_PeerClientSide.Object);
            Assert.Empty(clientRoom.Entities);
            Assert.Single(serverRoom.Entities);

            // Sync entity from server to client
            for (int i = 0; i < RailConfig.SERVER_SEND_RATE + RailConfig.CLIENT_SEND_RATE + 1; ++i)
            {
                m_ConClientSide.ExecuteSends();
                m_Server.Update();
                m_ConServerSide.ExecuteSends();
                m_Client.Update();
            }

            // The client has received the entity.
            Assert.Single(clientRoom.Entities);
            Assert.Single(serverRoom.Entities);

            // Clients representation of the entity is identical to the server
            RailEntityBase entityProxy = clientRoom.Entities.First();
            Assert.IsType<RailEntityClient<SomeState>>(entityProxy);
            RailEntityClient<SomeState> entityClientSide = entityProxy as RailEntityClient<SomeState>;
            Assert.NotNull(entityClientSide);
            Assert.Equal(entityServerSide.Id, entityProxy.Id);
            Assert.Equal(expectedTimeControl, entityServerSide.State.Mode);
            Assert.Equal(expectedTimeControl, entityClientSide.State.Mode);

            // Change the entity on server side and sync to the client
            expectedTimeControl = CampaignTimeControlMode.Stop;
            entityServerSide.State.Mode = expectedTimeControl;

            // Let the server detect the change and send the packet
            bool bWasSendTick = false;
            while (!bWasSendTick)
            {
                m_Server.Update();
                bWasSendTick = serverRoom.Tick.IsSendTick(RailConfig.SERVER_SEND_RATE);
            }

            // Let the client receive & process the packet. We need to bring the client up to the same tick as the server to see the result.
            while (clientRoom.Tick < serverRoom.Tick)
            {
                m_ConServerSide.ExecuteSends();
                m_Client.Update();
            }

            Assert.Equal(expectedTimeControl,  entityServerSide.State.Mode);
            Assert.Equal(expectedTimeControl,  entityClientSide.State.Mode);
        }
    }
}
