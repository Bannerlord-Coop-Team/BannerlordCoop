using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.NetImpl.LiteNet
{
    public class LiteNetP2PClient
    {
        EventBasedNetListener listener;
        NetManager netManager;
        public LiteNetP2PClient()
        {
            listener = new EventBasedNetListener();

            netManager = new NetManager(listener);

            netManager.Start();

            netManager.NatPunchEnabled = true;

            netManager.NatPunchModule.SendNatIntroduceRequest("68.117.137.52", 5565, "");
        }

        public void Connect(string Ip, int port)
        {
        }

        private void Listener_NatIntroductionRequest(System.Net.IPEndPoint localEndPoint, System.Net.IPEndPoint remoteEndPoint, string token)
        {
            throw new NotImplementedException();
        }
    }
}
