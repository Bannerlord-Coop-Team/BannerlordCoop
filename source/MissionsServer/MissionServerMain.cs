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
                if(messageType == MessageType.PlayerSync)
                {
                    byte[] serializedLocation = null;
                    dataReader.GetBytes(serializedLocation, dataReader.Position, dataReader.RawDataSize - dataReader.Position);
                    MemoryStream stream = new MemoryStream();
                    ClientTickMessage message = Serializer.DeserializeWithLengthPrefix<ClientTickMessage>(stream, PrefixStyle.Fixed32BigEndian);
                    playerSyncDict[fromPeer.Id] = message.AgentsTickInfo;
                }

            };

            List<ServerTickPayload> GeneratePlayerPayload(HashSet<int> clientIds)
            {
                List<ServerTickPayload> payloadList = new List<ServerTickPayload>();

                foreach(int clientId in clientIds)
                {
                    List<PlayerTickInfo> syncInfo = new List<PlayerTickInfo>();
                    ServerTickPayload payload = new ServerTickPayload();
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
                MemoryStream stream = new MemoryStream();

                ServerTickMessage message = new ServerTickMessage();
                foreach (KeyValuePair<string, HashSet<int>> locationKVP in clientsLocation) 
                {
                    // player exist in this location, sync them
                    if(locationKVP.Value.Count > 0)
                    {
                        // client ids in this location
                        HashSet<int> clientIds = locationKVP.Value;

                        // get all client info based on clients in same location
                        List<ServerTickPayload> payload = GeneratePlayerPayload(clientIds);

                        // loop through clients and update them
                        foreach (int clientId in clientIds)
                        {
                            message.ClientTicks = payload;
                        }
                    }
                }
                Serializer.Serialize(stream, message);

                NetDataWriter dataWriter = new NetDataWriter();
                dataWriter.Put((uint)MessageType.PlayerSync);
                dataWriter.Put(stream.ToArray());
                server.SendToAll(dataWriter, DeliveryMethod.Sequenced);
                
                Thread.Sleep(15);
            }
            server.Stop();
        }
    }
}
