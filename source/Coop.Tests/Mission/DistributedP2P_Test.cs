using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Coop.Mod.Config;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using LiteNetLib.Utils;
using Xunit;

namespace Coop.Tests.Mission
{

    public class DistributedP2P_Test : IDisposable
    {
        List<LiteNetP2PClient> clients = new List<LiteNetP2PClient>();
        LiteNetP2PServer server;

        DefaultNetworkConfig config = new DefaultNetworkConfig();

        public void Dispose()
        {
            server.Stop();
            foreach (var client in clients)
            {
                client.Stop();
            }
        }

        [Fact]
        public void SendData_Test()
        {
            server = new LiteNetP2PServer(config);
            LiteNetP2PClient client1 = new LiteNetP2PClient(config);
            LiteNetP2PClient client2 = new LiteNetP2PClient(config);

            clients.Add(client1);
            clients.Add(client2);

            TimeSpan updateTime = TimeSpan.FromSeconds(1);

            Assert.True(client1.ConnectToP2PServer("test1"));
            Assert.True(client2.ConnectToP2PServer("test1"));

            DateTime startTime = DateTime.Now;

            while (DateTime.Now - startTime < updateTime)
            {
                server.Update();
                client1.Update();
                client2.Update();
                Thread.Sleep(10);
            }

            string sentStr = "hi";
            int calls = 0;

            client2.DataRecieved += (sender, e, t) =>
            {
                calls += 1;

                string rxStr = e.GetString();

                Assert.Equal(sentStr, rxStr);
            };

            NetDataWriter writer = new NetDataWriter();
            writer.Put(sentStr);
            client1.SendAll(writer, DeliveryMethod.ReliableSequenced);

            startTime = DateTime.Now;

            while (DateTime.Now - startTime < updateTime)
            {
                server.Update();
                client1.Update();
                client2.Update();
                Thread.Sleep(10);
            }

            Assert.Equal(1, calls);
        }
        

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(60)]
        public void N_P2PClients_Test(int N)
        {
            server = new LiteNetP2PServer(config);

            int expectedConnections = (N - 1);

            for (int i = 0; i < N; i++)
            {
                LiteNetP2PClient client = new LiteNetP2PClient(config);
                clients.Add(client);
                client.ConnectToP2PServer("test");
            }

            DateTime startTime = DateTime.Now;

            TimeSpan updateTime = TimeSpan.FromSeconds(1);

            while (DateTime.Now - startTime < updateTime &&
                   clients.Any(c => c.ConnectedPeersCount < expectedConnections))
            {
                server.Update();
                for (int i = 0; i < N; i++)
                {
                    clients[i].Update();
                }
                Thread.Sleep(10);
            }

            Assert.Equal(N, server.ConnectedPeersCount);

            Assert.True(clients.All(c => c.ConnectedPeersCount == expectedConnections));

            int removeAmount = (int)(N / 2);

            for (int i = 0;i < removeAmount; i++)
            {
                var c = clients[i];
                c.Stop();
                clients.RemoveAt(i);
            }

            startTime = DateTime.Now;

            while (DateTime.Now - startTime < updateTime &&
                   clients.All(c => c.ConnectedPeersCount > expectedConnections - removeAmount))
            {
                server.Update();
                foreach(var client in clients)
                {
                    client.Update();
                }

                Thread.Sleep(10);
            }

            Assert.Equal(N - removeAmount, server.ConnectedPeersCount);

            Assert.All(clients, c => Assert.Equal(expectedConnections - removeAmount, c.ConnectedPeersCount));
        }
    }
}
