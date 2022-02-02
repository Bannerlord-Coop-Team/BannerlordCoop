using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MissionsServerTest
{
    internal class MissionsServerTest
    {
        static void Main(string[] args)
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager client = new NetManager(listener);
            client.Start();
            client.Connect("localhost" /* host ip or name */, 9050 /* port */, "SomeConnectionKey" /* text key or NetDataWriter */);
            listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                Console.WriteLine("We got: {0}", dataReader.GetString(100 /* max length of string */));
                dataReader.Recycle();
                client.SendToAll(new byte[] { 5 }, DeliveryMethod.Sequenced);
                Console.WriteLine("We should have sent: " + client.ConnectedPeersCount);
            };


            

            while (!Console.KeyAvailable)
            {
                client.PollEvents();
                Thread.Sleep(15); // approx. 60hz
            }

            client.Stop();
        }
    }
}
