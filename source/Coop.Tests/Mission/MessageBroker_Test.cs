using Common;
using Common.Messaging;
using Coop.Mod.Config;
using Coop.Mod.Mission.Network;
using Coop.NetImpl.LiteNet;
using Coop.Tests.Mission.Dummy;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Mission
{
    public class MessageBroker_Test : IDisposable
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
        public void SendEventMessage()
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
                client1.Update(TimeSpan.Zero);
                client2.Update(TimeSpan.Zero);
                Thread.Sleep(10);
            }

            Assert.Equal(1, client1.ConnectedPeersCount);
            Assert.Equal(1, client2.ConnectedPeersCount);


            IMessageBroker broker = new MessageBroker(client1);
            IMessageBroker broker2 = new MessageBroker(client2);

            int Client1Calls = 0;
            int Client2Calls = 0;

            int value1 = 1;
            int value2 = 5;

            Action<MessagePayload<int>> rxEvent1 = ((m) =>
            {
                Assert.Equal(value2, m.What);
                Client1Calls++;
            });

            Action<MessagePayload<int>> rxEvent2 = ((m) =>
            {
                Assert.Equal(value1, m.What);
                Client2Calls++;
            });

            broker.Subscribe(rxEvent1);
            broker2.Subscribe(rxEvent2);

            broker.Publish("c1", value1);
            broker2.Publish("c2", value2);

            startTime = DateTime.Now;

            while (DateTime.Now - startTime < updateTime)
            {
                server.Update();
                client1.Update(TimeSpan.Zero);
                client2.Update(TimeSpan.Zero);
                Thread.Sleep(10);
            }

            Assert.Equal(1, Client1Calls);
            Assert.Equal(1, Client2Calls);

            // Publish invalid message
            Assert.Throws<SerializationException>(() => { broker.Publish("c1", broker); });
        }
    }
}
