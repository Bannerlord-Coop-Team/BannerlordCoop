using LiteNetLib;
using LiteNetLib.Utils;
using MiscUtil.IO;
using MissionsShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MissionsServer
{
    internal class MissionsServerMain
    {
        HashSet<int> clientIdsInLordsHall = new HashSet<int>();
        Dictionary<int, PlayerTickSync> playerSync = new Dictionary<int, PlayerTickSync>();
        static void Main(string[] args)
        {

            PlayerTickSync sync = new PlayerTickSync(1f, 2f, 3f, 4, 5, 6f, 7f, 8f, 9, 10, 11, 12f, 13, 14, 15, 16f, 17f, 18f, true);
            
            MemoryStream ms = new MemoryStream();
            
            byte [] data = sync.Serialize(ms);

            PlayerTickSync sync2 = new PlayerTickSync();
            sync2.Deserialize(data);
            Console.WriteLine(sync2.ToString());

            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager server = new NetManager(listener);
            server.Start(9050 /* port */);

            listener.ConnectionRequestEvent += request =>
            {
                if (server.ConnectedPeersCount < 10 /* max connections */)
                    request.AcceptIfKey("SomeConnectionKey");
                else
                    request.Reject();
            };

            listener.PeerConnectedEvent += peer =>
            {
                Console.WriteLine("We got connection: {0}", peer.EndPoint); // Show peer ip
                NetDataWriter writer = new NetDataWriter();                 // Create writer class
                writer.Put("Hello client!");                                // Put some string
                peer.Send(writer, DeliveryMethod.ReliableOrdered);             // Send with reliability
                
            };

            listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                Console.WriteLine("We got: " + dataReader.AvailableBytes);

            };

           

            


            while (!Console.KeyAvailable)
            {
                server.PollEvents();
                Thread.Sleep(15);
            }
            server.Stop();
        }
    }
}
