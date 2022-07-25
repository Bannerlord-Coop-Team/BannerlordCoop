using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using LiteNetLib.Utils;
using Xunit;

namespace Coop.Tests.Mission
{

    public class DistributedP2P_Test
    {
        LiteNetP2PServer server = new LiteNetP2PServer();
        EventBasedNetListener client1_listener = new EventBasedNetListener();
        EventBasedNetListener client2_listener = new EventBasedNetListener();

        [Fact]
        public void ConnectionTest()
        {
            while (true)
            {
                server.Update();
                Thread.Sleep(10);
            }
        }
    }
}
