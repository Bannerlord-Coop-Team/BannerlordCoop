using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Serialization;
using Coop.Mod.Config;
using Coop.NetImpl.LiteNet;
using Coop.Tests.Missions.Dummy;
using Coop.Tests.Missions.P2PUtils;
using LiteNetLib;
using LiteNetLib.Utils;
using Network.Infrastructure;
using ProtoBuf;
using Xunit;

namespace Coop.Tests.Missions
{

    public class DistributedP2P_Test : IDisposable
    {
        P2PGroup group = new P2PGroup("Test");

        public void Dispose()
        {
            group.Dispose();
        }

        [ProtoContract(SkipConstructor = true)]
        class TestPacket : IPacket
        {
            public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableSequenced;
            public PacketType PacketType => PacketType.Event;
            public byte[] Data => m_Data;
            [ProtoMember(1)]

            private byte[] m_Data;

            public TestPacket(string str)
            {
                m_Data = CommonSerializer.Serialize(str);
            }
        }

        class TestPacketHandler : IPacketHandler
        {
            public PacketType PacketType => PacketType.Event;
            private Action<IPacket> m_Callback;

            public TestPacketHandler(Action<IPacket> callback)
            {
                m_Callback = callback;
            }

            public void HandlePacket(NetPeer peer, IPacket packet)
            {
                m_Callback.Invoke(packet);
            }

            public void HandlePeerDisconnect(NetPeer peer, DisconnectInfo reason)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void PacketHandler_Test()
        {
            var res = group.Connect2Clients();

            LiteNetP2PClient client1 = res.Item1;
            LiteNetP2PClient client2 = res.Item2;

            TimeSpan updateTime = TimeSpan.FromSeconds(1);

            int calls = 0;
            string sendVal = "hi";

            var handler = new TestPacketHandler((p) =>
            {
                calls += 1;
                Assert.Equal(sendVal, CommonSerializer.Deserialize<string>(p.Data));
            });

            client2.AddHandler(handler);

            TestPacket packet = new TestPacket(sendVal);
            client1.SendAll(packet);

            group.UpdateForXTime(updateTime, (_) => { return calls > 0; });
        }

        [Fact]
        public void SendData_Test()
        {
            var res = group.Connect2Clients();

            LiteNetP2PClient client1 = res.Item1;
            LiteNetP2PClient client2 = res.Item2;

            TimeSpan updateTime = TimeSpan.FromSeconds(1);

            // Client 1 to client 2
            string sentStr = "hi";
            int c2Calls = 0;

            TestPacketHandler handler2 = new TestPacketHandler((p) =>
            {
                c2Calls += 1;

                string rxStr = CommonSerializer.Deserialize<string>(p.Data);

                Assert.Equal(sentStr, rxStr);
            });

            client2.AddHandler(handler2);

            TestPacket packet = new TestPacket(sentStr);
            client1.SendAll(packet);

            group.UpdateForXTime(updateTime, (_) => { return c2Calls > 0; });

            Assert.Equal(1, c2Calls);

            // Client 2 to client 1
            int c1Calls = 0;

            TestPacketHandler handler1 = new TestPacketHandler((p) =>
            {
                c1Calls += 1;

                string rxStr = CommonSerializer.Deserialize<string>(p.Data);

                Assert.Equal(sentStr, rxStr);
            });

            client1.AddHandler(handler1);

            packet = new TestPacket(sentStr);
            client2.SendAll(packet);

            group.UpdateForXTime(updateTime, (_) => { return c1Calls > 0; });

            Assert.Equal(1, c1Calls);
        }
        

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(60)]
        public void N_P2PClients_Test(int N)
        {
            group.AddServer();

            for(int i = 0; i < N; i++)
            {
                group.AddClient();
            }
            
            int expectedConnections = N - 1;

            TimeSpan updateTime = TimeSpan.FromSeconds(1);

            group.UpdateForXTime(updateTime, (c) => { return c.ConnectedPeersCount >= expectedConnections; });

            Assert.Equal(N, group.Server.NetManager.ConnectedPeersCount);

            Assert.True(group.Clients.All(c => c.ConnectedPeersCount == expectedConnections));

            int removeAmount = (N / 2);

            for (int i = 0; i < removeAmount; i++)
            {
                var c = group.Clients[i];
                c.Stop();
                group.Clients.RemoveAt(i);
            }

            group.UpdateForXTime(updateTime, (c) => { return c.ConnectedPeersCount <= expectedConnections - removeAmount; });

            Assert.Equal(N - removeAmount, group.Server.NetManager.ConnectedPeersCount);

            Assert.All(group.Clients, c => Assert.Equal(expectedConnections - removeAmount, c.ConnectedPeersCount));
        }
    }
}
