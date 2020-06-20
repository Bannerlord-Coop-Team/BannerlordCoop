using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Extensions.Data;
using JetBrains.Annotations;
using Network;
using Network.Infrastructure;
using Network.Protocol;
using NLog;

namespace Sync.Store
{
    public struct ObjectId
    {
        public uint Value { get; }

        public ObjectId(uint id)
        {
            Value = id;
        }
    }

    public class RemoteObject
    {
        public object Object { get; set; }
        public bool Sent { get; set; }
        public bool Acknowledged { get; set; }
    }

    /// <summary>
    ///     Stores arbitrary data that is synchronized to a remote instance of this class.
    ///     Attention: Data added to the store will not be automatically removed! It will be kept
    ///     until it is explicitly removed.
    /// </summary>
    public class RemoteStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConnectionBase m_Connection;

        private readonly Dictionary<ObjectId, RemoteObject> m_Data;

        /// <summary>
        ///     Triggered when the remote instance confirmed the reception of a sent object.
        /// </summary>
        public Action<ObjectId, object> OnObjectAcknowledged;

        /// <summary>
        ///     Triggered when the local instance received an object from the remote instance.
        /// </summary>
        public Action<ObjectId, object> OnObjectReceived;

        /// <summary>
        ///     Creates a new store.
        /// </summary>
        /// <param name="data">data storage for all objects in this store.</param>
        /// <param name="connection">connection to be used to communicate with the remote store</param>
        public RemoteStore(
            [NotNull] Dictionary<ObjectId, RemoteObject> data,
            [NotNull] ConnectionBase connection)
        {
            m_Data = data;
            m_Connection = connection;
            m_Connection.Dispatcher.RegisterPacketHandlers(this);
        }

        public IReadOnlyDictionary<ObjectId, RemoteObject> Data => m_Data;

        ~RemoteStore()
        {
            m_Connection.Dispatcher.UnregisterPacketHandlers(this);
        }

        [PacketHandler(EConnectionState.ClientAwaitingWorldData, EPacket.StoreAdd)]
        [PacketHandler(EConnectionState.ClientPlaying, EPacket.StoreAdd)]
        [PacketHandler(EConnectionState.ServerJoining, EPacket.StoreAdd)]
        [PacketHandler(EConnectionState.ServerSendingWorldData, EPacket.StoreAdd)]
        [PacketHandler(EConnectionState.ServerPlaying, EPacket.StoreAdd)]
        private void ReceiveAdd(Packet packet)
        {
            // Receive the object
            byte[] raw = packet.Payload.ToArray();
            ObjectId id = new ObjectId(XXHash.XXH32(raw));
            m_Data[id] = new RemoteObject
            {
                Object = Deserialize(raw),
                Sent = false,
                Acknowledged = true
            };

            // Return ACK
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(id.Value);
            m_Connection.Send(new Packet(EPacket.StoreAck, writer.ToArray()));

            Logger.Trace("Received {}: {}", id, m_Data[id].Object);
            OnObjectReceived?.Invoke(id, m_Data[id].Object);
        }

        [PacketHandler(EConnectionState.ClientAwaitingWorldData, EPacket.StoreAck)]
        [PacketHandler(EConnectionState.ClientPlaying, EPacket.StoreAck)]
        [PacketHandler(EConnectionState.ServerJoining, EPacket.StoreAck)]
        [PacketHandler(EConnectionState.ServerSendingWorldData, EPacket.StoreAck)]
        [PacketHandler(EConnectionState.ServerPlaying, EPacket.StoreAck)]
        private void ReceiveAck(Packet packet)
        {
            ObjectId id = new ObjectId(new ByteReader(packet.Payload).Binary.ReadUInt32());
            if (!m_Data.ContainsKey(id))
            {
                throw new Exception($"Received ACK for unknown object {id}.");
            }

            m_Data[id].Acknowledged = true;
            Logger.Trace("Receivec ACK {}: {}", id, m_Data[id].Object);
            OnObjectAcknowledged?.Invoke(id, m_Data[id].Object);
        }

        public ObjectId Insert(object obj)
        {
            byte[] raw = Serialize(obj);
            ObjectId id = new ObjectId(XXHash.XXH32(raw));
            m_Connection.Send(new Packet(EPacket.StoreAdd, raw));
            m_Data[id] = new RemoteObject
            {
                Object = obj,
                Acknowledged = false,
                Sent = true
            };
            Logger.Trace("Insert {}: {}", id, obj);
            return id;
        }

        public bool Remove(ObjectId id)
        {
            if (!m_Data.ContainsKey(id))
            {
                throw new ArgumentException($"Unknown key {id}", nameof(id));
            }

            Logger.Trace("Remove {}: {}", id, m_Data[id]);
            return m_Data.Remove(id);
        }

        private byte[] Serialize(object obj)
        {
            IFormatter formatter = new BinaryFormatter();
            MemoryStream stream = new MemoryStream();
            formatter.Serialize(stream, obj);
            return stream.ToArray();
        }

        private object Deserialize(byte[] raw)
        {
            MemoryStream buffer = new MemoryStream(raw);
            return new BinaryFormatter().Deserialize(buffer);
        }
    }
}
