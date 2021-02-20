using System;
using System.Collections.Generic;
using System.Linq;
using Extensions.Data;
using JetBrains.Annotations;
using Network;
using Network.Infrastructure;
using Network.Protocol;
using NLog;

namespace Sync.Store
{
    public class RemoteObjectState
    {
        public enum EOrigin
        {
            Local,
            Remote
        }

        public RemoteObjectState(EOrigin eOrigin)
        {
            Origin = eOrigin;
        }

        public EOrigin Origin { get; }
        public bool Sent { get; set; }
        public bool Acknowledged { get; set; }
    }

    /// <summary>
    ///     Stores arbitrary data that is synchronized to a remote instance of a store.
    ///     Attention: Data added to the store will not be automatically removed! It will be kept
    ///     until it is explicitly removed.
    /// </summary>
    public class RemoteStore : IStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConnectionBase m_Connection;
        private readonly Dictionary<ObjectId, object> m_Data;
        private readonly StoreSerializer m_Serializer;

        private readonly Dictionary<ObjectId, RemoteObjectState> m_State =
            new Dictionary<ObjectId, RemoteObjectState>();

        /// <summary>
        ///     Triggered when the remote instance confirmed the reception of a sent object.
        /// </summary>
        public Action<ObjectId, object> OnObjectAcknowledged;

        /// <summary>
        ///     Triggered when the local instance received an object from the remote instance and
        ///     an acknowledge has been sent back.
        /// </summary>
        public Action<ObjectId, object> OnObjectReceived;

        /// <summary>
        ///     Triggered when the local instance deserialized an added object from the remote store.
        ///     The return value determines if an ACK is sent back.
        /// </summary>
        public Func<ObjectId, byte[], object, bool> OnPacketAddDeserialized;

        /// <summary>
        ///     Creates a new store.
        /// </summary>
        /// <param name="data">data storage for all objects in this store.</param>
        /// <param name="connection">connection to be used to communicate with the remote store</param>
        public RemoteStore(
            [NotNull] Dictionary<ObjectId, object> data,
            [NotNull] ConnectionBase connection,
            [NotNull] ISerializableFactory serializableFactory)
        {
            m_Serializer = new StoreSerializer(serializableFactory);
            m_Data = data;
            m_Connection = connection;
            m_Connection.Dispatcher.RegisterPacketHandler(ReceiveAdd);
            m_Connection.Dispatcher.RegisterPacketHandler(ReceiveAck);
        }

        public IReadOnlyDictionary<ObjectId, RemoteObjectState> State => m_State;

        public ObjectId Insert(object obj)
        {
            var raw = m_Serializer.Serialize(obj);
            var id = new ObjectId(XXHash.XXH32(raw));
            m_Data[id] = obj;
            Logger.Trace("[{id}] Insert: {object} [{type}]", id, obj, obj.GetType());
            SendAdd(id, raw);
            return id;
        }

        public bool Remove(ObjectId id)
        {
            m_State.Remove(id);
            Logger.Trace("[{id}] Remove: {object} [{type}]", id, m_Data[id], m_Data[id].GetType());
            return m_Data.Remove(id);
        }

        public IReadOnlyDictionary<ObjectId, object> Data => m_Data;

        ~RemoteStore()
        {
            m_Connection.Dispatcher.UnregisterPacketHandlers(this);
        }

        [ConnectionClientPacketHandler(EClientConnectionState.Connected, EPacket.StoreAdd)]
        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.StoreAdd)]
        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.StoreAdd)]
        private void ReceiveAdd(ConnectionBase connection, Packet packet)
        {
            // Receive the object
            var raw = packet.Payload.ToArray();
            var id = new ObjectId(XXHash.XXH32(raw));
            m_State[id] = new RemoteObjectState(RemoteObjectState.EOrigin.Remote);

            // Add to store
            if (m_Data.ContainsKey(id))
            {
                Logger.Warn(
                    "[{id}]: {object} [{type}] already stored. Objects should only be added once!",
                    id,
                    m_Data[id],
                    m_Data[id]);
            }
            else
            {
                m_Data[id] = m_Serializer.Deserialize(raw);
                Logger.Trace(
                    "[{id}] Received: {object} [{type}]",
                    id,
                    m_Data[id],
                    m_Data[id].GetType());
            }

            // Call handlers
            var bDoSendAck = true;
            if (OnPacketAddDeserialized != null) bDoSendAck = OnPacketAddDeserialized.Invoke(id, raw, m_Data[id]);

            if (bDoSendAck)
            {
                SendACK(id);
                OnObjectReceived?.Invoke(id, m_Data[id]);
            }
        }

        public void SendACK(ObjectId id)
        {
            if (!m_State.ContainsKey(id)) throw new Exception($"Invalid internal state for {id}: Unknown.");

            if (m_State[id].Origin == RemoteObjectState.EOrigin.Local)
                throw new Exception(
                    "Invalid internal state for {id}: A locally added object cannot be acknowledged.");

            var writer = new ByteWriter();
            writer.Binary.Write(id.Value);
            m_State[id].Acknowledged = true;
            m_Connection.Send(new Packet(EPacket.StoreAck, writer.ToArray()));
            Logger.Trace("[{id}] Sent ACK", id);
        }

        [ConnectionClientPacketHandler(EClientConnectionState.Connected, EPacket.StoreAck)]
        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.StoreAck)]
        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.StoreAck)]
        private void ReceiveAck(ConnectionBase connection, Packet packet)
        {
            var id = new ObjectId(new ByteReader(packet.Payload).Binary.ReadUInt32());
            if (!m_Data.ContainsKey(id) || !m_State.ContainsKey(id))
                throw new Exception($"Received ACK for unknown object {id}.");

            m_State[id].Acknowledged = true;
            Logger.Trace(
                "[{id}] Received ACK: {object} [{type}]",
                id,
                m_Data[id],
                m_Data[id].GetType());
            OnObjectAcknowledged?.Invoke(id, m_Data[id]);
        }

        public void SendAdd(ObjectId id, byte[] raw)
        {
            m_Connection.Send(new Packet(EPacket.StoreAdd, raw));
            m_State[id] = new RemoteObjectState(RemoteObjectState.EOrigin.Local)
            {
                Acknowledged = false,
                Sent = true
            };

            Logger.Trace("[{id}] Sent StoreAdd", id);
        }
    }
}