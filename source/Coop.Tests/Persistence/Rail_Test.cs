using Coop.Game.Persistence;
using Coop.Multiplayer.Network;
using Moq;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using Xunit;

namespace Coop.Tests
{
    public class Rail_Test
    {
        private readonly RailClient m_Client;
        private readonly RailServer m_Server;

        private Mock<RailNetPeerWrapper> m_PeerClientSide;
        private Mock<RailNetPeerWrapper> m_PeerServerSide;

        private readonly InMemoryConnection m_ConClientSide = new InMemoryConnection();
        private readonly InMemoryConnection m_ConServerSide = new InMemoryConnection();

        private readonly TestEnvironment m_Environment = new TestEnvironment();

        public Rail_Test()
        {
            Environment.Current = m_Environment;
            m_Client = new RailClient(Registry.Get(Component.Client));
            m_Server = new RailServer(Registry.Get(Component.Server));
            

            m_PeerClientSide = new Mock<RailNetPeerWrapper>(m_ConServerSide)
            {
                CallBase = true
            };
            m_PeerServerSide = new Mock<RailNetPeerWrapper>(m_ConClientSide)
            {
                CallBase = true
            };

            m_ConClientSide.OnSend += m_PeerServerSide.Object.Receive;
            m_ConServerSide.OnSend += m_PeerClientSide.Object.Receive;
        }

        [Fact]
        void ClientServerCommunication()
        {
            // Init
            m_Client.StartRoom();
            RailPopulator.Populate(m_Server.StartRoom());
            m_Client.SetPeer(m_PeerClientSide.Object);
            m_Server.AddClient(m_PeerServerSide.Object, "Test");

            m_Server.Update();
            m_Client.Update();
        }
    }
}
