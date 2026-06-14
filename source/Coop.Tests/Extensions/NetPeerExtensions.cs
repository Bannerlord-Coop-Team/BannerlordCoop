using Common.Util;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library.NewsManager;

namespace Coop.Tests.Extensions
{
    internal static class NetPeerExtensions
    {
        private static readonly FieldInfo Id = typeof(NetPeer).GetField(nameof(NetPeer.Id))!;
        private static readonly FieldInfo _channels = typeof(NetPeer).GetField("_channels", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly ConstructorInfo ctor = typeof(NetPeer).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, new Type[]
        {
            typeof(NetManager),
            typeof(IPEndPoint),
            typeof(int),
        })!;
        public static void SetId(this NetPeer peer, int id)
        {
            Id.SetValue(peer, id);
        }

        public static void Setup(this NetPeer peer, int id, string iPAddress = "127.0.0.1")
        {
            var manager = new NetManager(null);

            var endPoint = new IPEndPoint(IPAddress.Parse(iPAddress), 5555);

            ctor.Invoke(peer, new object[] { manager, endPoint, id });
        }
        private static Assembly LiteNetLibAsm = typeof(NetPeer).Assembly;
        private static Type NetPacketType = LiteNetLibAsm.GetType("LiteNetLib.NetPacket", true)!;
        private static object CreatePacket => ObjectHelper.SkipConstructor(NetPacketType);

        public static void SetQueueLength(this NetPeer peer, int queueLength)
        {
            Type BaseChannelType = LiteNetLibAsm.GetType("LiteNetLib.BaseChannel", true)!;
            Type ReliableChannelType = LiteNetLibAsm.GetType("LiteNetLib.ReliableChannel", true)!;


            FieldInfo OutgoingQueue = BaseChannelType.GetField("OutgoingQueue", BindingFlags.NonPublic | BindingFlags.Instance)!;
            
            Type QueueType = typeof(Queue<>).MakeGenericType(NetPacketType);
            MethodInfo Enqueue = QueueType.GetMethod("Enqueue")!;
            MethodInfo Dequeue = QueueType.GetMethod("Clear")!;
            MethodInfo Array_GetValue = typeof(Array).GetMethod(nameof(Array.GetValue), BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(int) })!;
            MethodInfo Array_SetValue = typeof(Array).GetMethod(nameof(Array.SetValue), BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(object), typeof(int) })!;


            var channels = _channels.GetValue(peer);
            var channel_0 = Activator.CreateInstance(ReliableChannelType, new object[] { peer, false, (byte)0 })!;
            Array_SetValue.Invoke(channels, new object[] { channel_0, 0 });

            var queue = Activator.CreateInstance(QueueType);

            for (int i = 0; i < queueLength; i++)
            {
                Enqueue.Invoke(queue, new object[] { CreatePacket });
            }

            OutgoingQueue.SetValue(channel_0, queue);
        }
    }
}
