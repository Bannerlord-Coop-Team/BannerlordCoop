using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LiteNetLib;

namespace Coop.Tests.Mission
{

    public class DistributedP2P_Test
    {
        EventBasedNetListener server_listener = new EventBasedNetListener();
        EventBasedNetListener client1_listener = new EventBasedNetListener();
        EventBasedNetListener client2_listener = new EventBasedNetListener();


        public void ConnectionTest()
        {

            NetManager s = new NetManager(server_listener);
            NetManager c1 = new NetManager(client1_listener);
            NetManager c2 = new NetManager(client2_listener);

            s.Start(4567);
            c1.Connect("localhost", 4567, "");
            c2.Connect("localhost", 4567, "");

            server_listener.ConnectionRequestEvent += (e) =>
            {
                e.Accept();
            };

            for(int i = 0; i < 10; i++)
            {
                s.PollEvents();
                c1.PollEvents();
                c2.PollEvents();
            }
        }
    }
}
