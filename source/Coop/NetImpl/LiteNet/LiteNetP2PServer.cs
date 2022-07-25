using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.NetImpl.LiteNet
{
    public class LiteNetP2PServer
    {
        readonly string _host = "68.117.137.52";
        readonly int _port = 5565;

        EventBasedNetListener listener;

        NetManager netManager;

        public LiteNetP2PServer()
        {
            listener = new EventBasedNetListener();

            netManager = new NetManager(listener);

            listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;

            netManager.Start(_host, "", _port);
        }

        private void Listener_ConnectionRequestEvent(ConnectionRequest request)
        {
            request.Accept();
        }

        public void Update()
        {
            netManager.PollEvents();
        }
    }
}
