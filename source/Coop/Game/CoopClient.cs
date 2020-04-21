using Coop.Common;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using System;
using System.Net;

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
            m_Session = new GameSession(new WorldData());
            m_Manager = new NetManagerClient(m_Session);
            GameState = new CoopGameState();
            Events = new CoopEvents();
        }
        public void TryConnect(IPAddress ip, int iPort)
        {
            m_Manager.Connect(ip.ToString(), iPort);
        }
        public void Update(TimeSpan frameTime)
        {
            m_Manager.Update(frameTime);
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
        private readonly NetManagerClient m_Manager;
    }
}
