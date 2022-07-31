using Common;
using Common.Messaging;
using Coop.Mod.Config;
using Coop.Mod.Missions.Network;
using Coop.NetImpl.LiteNet;
using Coop.Tests.Mission.Dummy;
using Coop.Tests.Mission.P2PUtils;
using LiteNetLib;
using LiteNetLib.Utils;
using Network.Infrastructure;
using ProtoBuf;
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
        P2PGroup group = new P2PGroup(nameof(MessageBroker_Test));


        public void Dispose()
        {
            group.Dispose();
        }

        [Fact]
        public void SendNativeEventMessage()
        {
            var res = group.Connect2Clients();

            LiteNetP2PClient client1 = res.Item1;
            LiteNetP2PClient client2 = res.Item2;

            TimeSpan updateTime = TimeSpan.FromSeconds(1);

            IMessageBroker broker = new NetworkMessageBroker(client1);
            IMessageBroker broker2 = new NetworkMessageBroker(client2);

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

            broker.Publish(value1);
            broker2.Publish(value2);

            group.UpdateForXTime(updateTime, (_) => { return Client1Calls == 1 && Client2Calls == 1; });

            Assert.Equal(1, Client1Calls);
            Assert.Equal(1, Client2Calls);
        }

        [ProtoContract]
        class MyProtoBufObj
        {
            [ProtoMember(1)]
            public int MyInt { get; set; }
        }

        [Fact]
        public void SendProtoBufEventMessage()
        {
            var res = group.Connect2Clients();

            LiteNetP2PClient client1 = res.Item1;
            LiteNetP2PClient client2 = res.Item2;

            TimeSpan updateTime = TimeSpan.FromSeconds(1);

            IMessageBroker broker = new NetworkMessageBroker(client1);
            IMessageBroker broker2 = new NetworkMessageBroker(client2);

            int Client1Calls = 0;
            int Client2Calls = 0;

            int value1 = 1;
            int value2 = 5;

            Action<MessagePayload<MyProtoBufObj>> rxEvent1 = ((m) =>
            {
                Assert.Equal(value2, m.What.MyInt);
                Client1Calls++;
            });

            Action<MessagePayload<MyProtoBufObj>> rxEvent2 = ((m) =>
            {
                Assert.Equal(value1, m.What.MyInt);
                Client2Calls++;
            });

            broker.Subscribe(rxEvent1);
            broker2.Subscribe(rxEvent2);

            MyProtoBufObj obj1 = new MyProtoBufObj()
            {
                MyInt = value1
            };

            MyProtoBufObj obj2 = new MyProtoBufObj()
            {
                MyInt = value2
            };

            broker.Publish(obj1);
            broker2.Publish(obj2);

            group.UpdateForXTime(updateTime, (_) => { return Client1Calls == 1 && Client2Calls == 1; });

            Assert.Equal(1, Client1Calls);
            Assert.Equal(1, Client2Calls);
        }

        [Fact]
        public void MessageNotSerializable()
        {
            var res = group.Connect2Clients();

            LiteNetP2PClient client1 = res.Item1;
            LiteNetP2PClient client2 = res.Item2;

            TimeSpan updateTime = TimeSpan.FromSeconds(1);

            IMessageBroker broker = new NetworkMessageBroker(client1);
            IMessageBroker broker2 = new NetworkMessageBroker(client2);

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

            // Publish invalid message
            Assert.Throws<InvalidOperationException>(() => { broker.Publish(broker); });
        }
    }
}
