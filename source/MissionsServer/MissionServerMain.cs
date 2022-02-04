using LiteNetLib;
using LiteNetLib.Utils;
using MiscUtil.IO;
using MissionsShared;
using ProtoBuf;
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
        
        static void Main(string[] args)
        {


            //each clients most updated location of its agents
            Dictionary<int, List<PlayerTickInfo>> playerSyncDict = new Dictionary<int, List<PlayerTickInfo>>();

            //location id maps to a set of clients
            Dictionary<string, HashSet<int>> clientsLocation = new Dictionary<string, HashSet<int>>(); 
            

            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager server = new NetManager(listener);


            //local test
            //FromClientTickMessage message = new FromClientTickMessage();
            //message.AgentCount = 1;
            //List<PlayerTickInfo> info = new List<PlayerTickInfo>();
            //PlayerTickInfo tick = new PlayerTickInfo();
            //tick.Action1Flag = 0x3U;
            //info.Add(tick);
            //message.AgentsTickInfo = info;
            //MemoryStream s = new MemoryStream();
            //Serializer.SerializeWithLengthPrefix(s, message, PrefixStyle.Fixed32BigEndian);

            //MemoryStream s2 = new MemoryStream(s.ToArray());
            //FromClientTickMessage recv = new FromClientTickMessage();
            //FromClientTickMessage rcvMessg = Serializer.DeserializeWithLengthPrefix<FromClientTickMessage>(s2, PrefixStyle.Fixed32BigEndian);
            //Console.WriteLine(rcvMessg.AgentsTickInfo.First().Action1Flag);


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
                MissionsShared.MessageType messageType = (MessageType)dataReader.GetUInt();
                if (messageType == MessageType.PlayerSync)
                {
                    byte[] serializedLocation = new byte[dataReader.RawDataSize - dataReader.Position];
                    Buffer.BlockCopy(dataReader.RawData, dataReader.Position, serializedLocation, 0, dataReader.RawDataSize - dataReader.Position);

                    MemoryStream stream = new MemoryStream(serializedLocation);
                    FromClientTickMessage msg = Serializer.DeserializeWithLengthPrefix<FromClientTickMessage>(stream, PrefixStyle.Fixed32BigEndian);
                    Console.WriteLine("Tick Value: " + msg.AgentsTickInfo.First().Action2Flag + "\n");
                    //playerSyncDict[fromPeer.Id] = msg.AgentsTickInfo;
                    playerSyncDict[fromPeer.Id] = msg.AgentsTickInfo;
                }

            };

            List<FromServerTickPayload> GeneratePlayerPayload(HashSet<int> clientIds)
            {
                List<FromServerTickPayload> payloadList = new List<FromServerTickPayload>();

                foreach(int clientId in clientIds)
                {
                    List<PlayerTickInfo> syncInfo = new List<PlayerTickInfo>();
                    FromServerTickPayload payload = new FromServerTickPayload();
                    payload.ClientId = clientId;
                    payload.AgentCount = syncInfo.Count;
                    payload.PlayerTick = syncInfo;
                    payloadList.Add(payload);
                }
                
                return payloadList;
            }


            while (!Console.KeyAvailable)
            {
                server.PollEvents();
                //MemoryStream stream = new MemoryStream();

                //FromServerTickMessage message = new FromServerTickMessage();
                //foreach (KeyValuePair<string, HashSet<int>> locationKVP in clientsLocation) 
                //{
                //    // player exist in this location, sync them
                //    if(locationKVP.Value.Count > 1)
                //    {
                //        // client ids in this location
                //        HashSet<int> clientIds = locationKVP.Value;

                //        // get all client info based on clients in same location
                //        List<FromServerTickPayload> payload = GeneratePlayerPayload(clientIds);

                //        // loop through clients and update them
                //        foreach (int clientId in clientIds)
                //        {
                //            message.ClientTicks = payload;
                //        }
                //    }
                //}
                //Serializer.Serialize(stream, message);

                //NetDataWriter dataWriter = new NetDataWriter();
                //dataWriter.Put((uint)MessageType.PlayerSync);
                //dataWriter.Put(stream.ToArray());
                //server.SendToAll(dataWriter, DeliveryMethod.Sequenced);
                
                Thread.Sleep(15);
            }
            server.Stop();
        }
    }
}
