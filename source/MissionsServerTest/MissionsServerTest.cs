using LiteNetLib;
using MissionsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using System.IO;

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
                //client.SendToAll(new byte[] { 5 }, DeliveryMethod.Sequenced);
                //Console.WriteLine("We should have sent: " + client.ConnectedPeersCount);
            };

            

            while (!Console.KeyAvailable)
            {
                client.PollEvents();
                Thread.Sleep(1000); // approx. 60hz
                FromClientTickMessage message = new FromClientTickMessage();
                List<PlayerTickInfo> agentsList = new List<PlayerTickInfo>();
                PlayerTickInfo mainParty = new PlayerTickInfo();
                mainParty.Action2Flag = 0x3F;
                agentsList.Add(mainParty);
                message.AgentsTickInfo = agentsList;
                MemoryStream stream = new MemoryStream();
                Serializer.SerializeWithLengthPrefix<FromClientTickMessage>(stream, message, PrefixStyle.Fixed32BigEndian);
                MemoryStream strm = new MemoryStream();
                message.AgentCount = 1;
                using (BinaryWriter writer = new BinaryWriter(strm))
                {
                    writer.Write((uint)MessageType.PlayerSync);
                    writer.Write(stream.ToArray());
                }





                client.SendToAll(strm.ToArray(), DeliveryMethod.ReliableOrdered);
            }

            client.Stop();
        }
    }
}
