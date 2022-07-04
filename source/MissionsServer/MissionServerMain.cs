using LiteNetLib;
using LiteNetLib.Utils;
using MiscUtil.IO;
using MissionsShared;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
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
            ConcurrentDictionary<int, List<PlayerTickInfo>> playerSyncDict = new ConcurrentDictionary<int, List<PlayerTickInfo>>();

            //location id maps to a set of clients
            ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> locationToClients = new ConcurrentDictionary<string, ConcurrentDictionary<int, byte>>();

            //id to location dictionary
            ConcurrentDictionary<int, string> clientToLocation = new ConcurrentDictionary<int, string>();
            

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
                //Console.WriteLine("We got connection: {0}", peer.EndPoint); // Show peer ip
                //NetDataWriter writer = new NetDataWriter();                 // Create writer class
                //writer.Put("Hello client!");                                // Put some string
                //peer.Send(writer, DeliveryMethod.ReliableOrdered);             // Send with reliability
                Console.WriteLine("Client Connected, assigned ID: " + peer.Id);
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.ConnectionId);
                writer.Put(peer.Id);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
                
            };

            listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                MissionsShared.MessageType messageType = (MessageType)dataReader.GetUInt();
                if(messageType == MessageType.EnterLocation)
                {
                    string locationName = dataReader.GetString();
                    if (!locationToClients.ContainsKey(locationName))
                    {
                        locationToClients[locationName] = new ConcurrentDictionary<int, byte>();
                    }
                    clientToLocation[fromPeer.Id] = locationName;
                    locationToClients[locationName][fromPeer.Id] = 1;
                    foreach (int clientId in locationToClients[locationName].Keys)
                    {
                        NetDataWriter writer = new NetDataWriter();
                        writer.Put((uint)MessageType.EnterLocation);
                        foreach(int cId in locationToClients[locationName].Keys)
                        {
                            writer.Put(cId);
                        }
                        Console.WriteLine("Sending: " + writer.Data.Length);
                        server.GetPeerById(clientId).Send(writer, DeliveryMethod.ReliableSequenced);

                    }
                }
                else if(messageType == MessageType.ExitLocation)
                {
                    string locationName = dataReader.GetString();
                    if (!locationToClients.ContainsKey(locationName))
                    {
                        return;
                    }
                    
                    foreach (PlayerTickInfo info in playerSyncDict[fromPeer.Id])
                    {
                        ServerAgentManager.Instance().RemoveAgent(info.Id);
                        Console.WriteLine("Client ID " + fromPeer.Id + " has removed agent: " + info.Id);
                    }
                    clientToLocation.TryRemove(fromPeer.Id, out _);
                    foreach (int clientId in locationToClients[locationName].Keys)
                    {
                        NetDataWriter writer = new NetDataWriter();
                        writer.Put((uint)MessageType.ExitLocation);
                        writer.Put(fromPeer.Id);
                        server.GetPeerById(clientId).Send(writer, DeliveryMethod.ReliableSequenced);

                    }
                    locationToClients[locationName].TryRemove(fromPeer.Id, out _);
                    playerSyncDict[fromPeer.Id].Clear();
                    playerSyncDict.TryRemove(fromPeer.Id, out _);
                }
                else if (messageType == MessageType.PlayerSync)
                {
                    byte[] serializedLocation = new byte[dataReader.RawDataSize - dataReader.Position];
                    Buffer.BlockCopy(dataReader.RawData, dataReader.Position, serializedLocation, 0, dataReader.RawDataSize - dataReader.Position);

                    MemoryStream stream = new MemoryStream(serializedLocation);
                    FromClientTickMessage msg = Serializer.DeserializeWithLengthPrefix<FromClientTickMessage>(stream, PrefixStyle.Fixed32BigEndian);
                    
                    //playerSyncDict[fromPeer.Id] = msg.AgentsTickInfo;
                    playerSyncDict[fromPeer.Id] = msg.AgentsTickInfo;
                    //Console.WriteLine("Received Update from: " + fromPeer.Id + " of # " + msg.AgentsTickInfo + " at location " + clientToLocation[fromPeer.Id]);
                }
                else if(messageType == MessageType.PlayerDamage)
                {
                    string location = clientToLocation[fromPeer.Id];
                    string effectedId = dataReader.GetString();
                    string effectorId = dataReader.GetString();
                    int damage = dataReader.GetInt();
                    NetDataWriter writer = new NetDataWriter();
                    writer.Put((uint)MessageType.PlayerDamage);
                    writer.Put(fromPeer.Id);
                    writer.Put(effectedId);
                    writer.Put(effectorId);
                    writer.Put(damage);
                    Console.WriteLine("Received damage from: " + fromPeer.Id + " to agent: " + effectedId + " from agent: " + effectorId + " of " + damage);
                    foreach(int client in locationToClients[location].Keys)
                    {
                        server.GetPeerById(client).Send(writer, DeliveryMethod.ReliableOrdered);
                    }
                }

                else if(messageType == MessageType.AddAgent)
                {
                    int senderClientId = fromPeer.Id;
                    int agentIndex = dataReader.GetInt();
                    string id = ServerAgentManager.Instance().GetAgentID(senderClientId, agentIndex);
                    NetDataWriter writer = new NetDataWriter();
                    writer.Put((uint)MessageType.AddAgent);
                    writer.Put(agentIndex);
                    writer.Put(id);
                    server.GetPeerById(fromPeer.Id).Send(writer, DeliveryMethod.ReliableOrdered);
                    Console.WriteLine(fromPeer.Id + " has added new agent with ID: " + id);
                   
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
                // receiver updates from clients 
                server.PollEvents();
                

                // get all the clients in each location
                foreach(ConcurrentDictionary<int, byte> clients in locationToClients.Values)
                {


                    // create stream for actual data
                    MemoryStream stream = new MemoryStream();

                    // create message from server payload to be sent to all clients in one location
                    FromServerTickMessage message = new FromServerTickMessage();


                    // get each clients id
                    foreach (int clientId in clients.Keys)
                    {
                        //grab the client's player tick info list
                        FromServerTickPayload payload = new FromServerTickPayload();
                        payload.ClientId = clientId;
                        if (!playerSyncDict.ContainsKey(clientId))
                        {
                            continue;
                        }
                        payload.PlayerTick = playerSyncDict[clientId];
                        message.ClientTicks.Add(payload);
                    }
                    Serializer.SerializeWithLengthPrefix<FromServerTickMessage>(stream, message, PrefixStyle.Fixed32BigEndian);
                    MemoryStream stream2 = new MemoryStream();

                    using (BinaryWriter writer = new BinaryWriter(stream2))
                    {
                        writer.Write((uint)MessageType.PlayerSync);
                        writer.Write(stream.ToArray());

                    }

                    foreach (int clientId in clients.Keys)
                    {
                        NetPeer peer = server.GetPeerById(clientId);
                        if (peer == null) return;
                        peer.Send(stream2.ToArray(), DeliveryMethod.Sequenced);
                    }
                }
                
                
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
