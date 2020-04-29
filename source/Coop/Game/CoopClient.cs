using Coop.Common;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using System;
using System.Net;
using Coop.Game.Persistence;
using RailgunNet;
using RailgunNet.Connection.Client;

namespace Coop.Game
{
    public class CoopClient : IUpdateable
    {
        private static readonly Lazy<CoopClient> m_Instance = new Lazy<CoopClient>(() => new CoopClient());
        public static CoopClient Instance 
        { 
            get 
            {
                return m_Instance.Value;
            } 
        }
        public CoopGameState GameState { get; private set; }
        public CoopEvents Events { get; private set; }
        private CoopClient()
        {
            m_Session = new GameSession(new SaveData());
            m_NetManager = new LiteNetManagerClient(m_Session);
            m_Persistence = new PersistenceClient();
            m_Session.OnConnectionCreated += m_Persistence.OnConnectionCreated;
            GameState = new CoopGameState();
            Events = new CoopEvents();
        }
        public void TryConnect(IPAddress ip, int iPort)
        {
            m_NetManager.Connect(ip.ToString(), iPort);
        }
        public void Update(TimeSpan frameTime)
        {
            m_NetManager.Update(frameTime);
            m_Persistence.Update(frameTime);
        }

        public override string ToString()
        {
            if(m_Session.Connection == null)
            {
                return "Client not connected.";
            }
            else
            {
                return m_Session.Connection.ToString();
            }
        }
        private readonly GameSession m_Session;
        private readonly LiteNetManagerClient m_NetManager;
        private readonly PersistenceClient m_Persistence;
    }
}
