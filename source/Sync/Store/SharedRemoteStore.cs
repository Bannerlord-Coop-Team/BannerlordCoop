using System;
using System.Collections.Generic;
using System.Linq;
using Extensions.Data;
using JetBrains.Annotations;
using Network.Infrastructure;
using NLog;

namespace Sync.Store
{
    /// <summary>
    ///     Implements a store that is synchronized to multiple remote stores trough different connections.
    /// </summary>
    public class SharedRemoteStore : IStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<ObjectId, object> m_Data = new Dictionary<ObjectId, object>();

        private readonly Dictionary<ObjectId, PendingResponse> m_PendingAcks =
            new Dictionary<ObjectId, PendingResponse>();

        private readonly StoreSerializer m_Serializer;

        private readonly Dictionary<ConnectionBase, RemoteStore> m_Stores =
            new Dictionary<ConnectionBase, RemoteStore>();

        /// <summary>
        ///     Triggered when an object has been distributed to all clients.
        /// </summary>
        public Action<ObjectId> OnObjectDistributed;

        /// <summary>
        ///     Triggered when an object has been distributed to all clients.
        /// </summary>
        public Action<ConnectionBase, object> OnObjectRecieved;

        public SharedRemoteStore([NotNull] ISerializableFactory serializableFactory)
        {
            m_Serializer = new StoreSerializer(serializableFactory);
        }

        public byte[] Serialize(object obj)
        {
            return m_Serializer.Serialize(obj);
        }

        public object Deserialize(byte[] raw)
        {
            return m_Serializer.Deserialize(raw);
        }

        public ObjectId Insert(object obj)
        {
            return Insert(obj, Serialize(obj));
        }

        public ObjectId Insert(object obj, byte[] serialized)
        {
            var id = new ObjectId(XXHash.XXH32(serialized));
            m_Data[id] = obj;

            m_PendingAcks[id] = new PendingResponse(null, m_Stores.Values.ToList());
            foreach (var store in m_Stores.Values) store.SendAdd(id, serialized);

            return id;
        }

        public bool Remove(ObjectId id)
        {
            m_PendingAcks.Remove(id);
            var bRemoved = m_Data.Remove(id);

            foreach (var store in m_Stores.Values) store.Remove(id);

            return bRemoved;
        }

        public IReadOnlyDictionary<ObjectId, object> Data => m_Data;

        public void AddConnection(ConnectionBase connection)
        {
            if (m_Stores.ContainsKey(connection))
                throw new Exception(
                    $"Cannot create two stores for the same connection {connection}.");

            var store = new RemoteStore(m_Data, connection, m_Serializer.Factory);
            store.OnPacketAddDeserialized += (id, payload, obj) =>
                RemoteObjectAdded(connection, id, payload, obj);
            store.OnObjectAcknowledged += (id, obj) => { ObjectAcknowledged(connection, id); };
            m_Stores.Add(connection, store);
        }

        private void ObjectAcknowledged(ConnectionBase sender, ObjectId id)
        {
            if (!m_Stores.ContainsKey(sender)) throw new Exception($"Unknown origin: {sender}.");

            if (!m_PendingAcks.ContainsKey(id))
            {
                Logger.Warn(
                    "[{id}] Received ACK from {sender}, but the server was not expecting one.",
                    id,
                    sender);
                return;
            }

            var pending = m_PendingAcks[id];
            pending.OnAckFrom(m_Stores[sender]);
            if (pending.AllDone())
            {
                pending.Origin?.SendACK(id);
                m_PendingAcks.Remove(id);
                OnObjectDistributed?.Invoke(id);
            }
        }

        private bool RemoteObjectAdded(
            ConnectionBase sender,
            ObjectId id,
            byte[] payload,
            object obj)
        {
            if (!m_Stores.ContainsKey(sender)) throw new Exception($"Unknown origin: {sender}.");

            OnObjectRecieved?.Invoke(sender, obj);

            var otherStores =
                m_Stores.Where(s => s.Key != sender).Select(p => p.Value).ToList();
            if (otherStores.Count == 0)
            {
                // Nothing more to do. Let the store send the ACK.
                OnObjectDistributed?.Invoke(id);
                return true;
            }

            m_PendingAcks[id] = new PendingResponse(m_Stores[sender], otherStores);
            foreach (var store in otherStores)
            {
                store.SendAdd(id, payload);
            }

            return false; // The ACK will be sent once the PendingResponse is done.
        }

        public void RemoveConnection(ConnectionBase connection)
        {
            m_Stores.Remove(connection);
        }

        private class PendingResponse
        {
            private readonly List<RemoteStore> m_Pending;

            public PendingResponse(
                [CanBeNull] RemoteStore origin,
                [NotNull] List<RemoteStore> storesToWaitFor)
            {
                Origin = origin;
                m_Pending = storesToWaitFor;
            }

            [CanBeNull] public RemoteStore Origin { get; }

            public void OnAckFrom(RemoteStore store)
            {
                m_Pending.Remove(store);
            }

            public bool AllDone()
            {
                return m_Pending.Count == 0;
            }
        }
    }
}