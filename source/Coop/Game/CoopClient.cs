using System;
using System.Net;
using Coop.Common;
using Coop.Game.Persistence;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;

namespace Coop.Game
{
    public class CoopClient : IUpdateable
    {
        private static readonly Lazy<CoopClient> m_Instance =
            new Lazy<CoopClient>(() => new CoopClient());

        private readonly LiteNetManagerClient m_NetManager;
        private readonly PersistenceClient m_Persistence;
        private readonly GameSession m_Session;

        private CoopClient()
        {
            m_Session = new GameSession(new SaveData());
            m_NetManager = new LiteNetManagerClient(m_Session);
            m_Persistence = new PersistenceClient();
            m_Session.OnConnectionCreated += m_Persistence.OnConnectionCreated;
            GameState = new CoopGameState();
            Events = new CoopEvents();
        }

        public static CoopClient Instance => m_Instance.Value;

        public CoopGameState GameState { get; }
        public CoopEvents Events { get; }

        public void Update(TimeSpan frameTime)
        {
            m_NetManager.Update(frameTime);
            m_Persistence.Update(frameTime);
        }

        public void TryConnect(IPAddress ip, int iPort)
        {
            m_NetManager.Connect(ip.ToString(), iPort);
        }

        public override string ToString()
        {
            if (m_Session.Connection == null)
            {
                return "Client not connected.";
            }

            return m_Session.Connection.ToString();
        }
    }
}
