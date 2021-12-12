using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace CoopTestMod
{
    internal class CustomGameNetworkHandler : IGameNetworkHandler
    {
        public void OnDisconnectedFromServer()
        {
            //throw new NotImplementedException();
        }

        public void OnEndMultiplayer()
        {
            //throw new NotImplementedException();
        }

        public void OnEndReplay()
        {
            //throw new NotImplementedException();
        }

        public void OnHandleConsoleCommand(string command)
        {
            //throw new NotImplementedException();
        }

        public void OnInitialize()
        {
            //throw new NotImplementedException();
        }

        public void OnNewPlayerConnect(PlayerConnectionInfo playerConnectionInfo, NetworkCommunicator networkPeer)
        {
            //throw new NotImplementedException();
        }

        public void OnPlayerDisconnectedFromServer(NetworkCommunicator peer)
        {
            //throw new NotImplementedException();
        }

        public void OnStartMultiplayer()
        {
            //throw new NotImplementedException();
        }

        public void OnStartReplay()
        {
            //throw new NotImplementedException();
        }
    }
}
