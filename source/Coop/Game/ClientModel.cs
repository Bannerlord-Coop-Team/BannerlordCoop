using Coop.Common;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using System;
using System.Net;
using TaleWorlds.Core;

namespace Coop.Game
{
    public class ClientModel : GameModel, IUpdateable
    {
        public CoopGameState GameState { get; private set; }
        public ClientModel()
        {
            m_Session = new ClientSession();
            m_Manager = new NetManagerClient(m_Session);
            GameState = new CoopGameState();
        }

        public void TryConnectToServer(IPAddress ip, int port)
        {
            m_Manager.Connect(ip.ToString(), port);
        }

        public void Update(TimeSpan frameTime)
        {
            m_Manager.Update(frameTime);
        }

        private readonly ClientSession m_Session;
        private readonly NetManagerClient m_Manager;
    }
}
