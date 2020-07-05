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

        private readonly Dictionary<ConnectionBase, RemoteStore> m_Stores =
            new Dictionary<ConnectionBase, RemoteStore>();

        public ObjectId Insert(object obj)
        {
            byte[] raw = StoreSerializer.Serialize(obj);
            ObjectId id = new ObjectId(XXHash.XXH32(raw));
            m_Data[id] = obj;
            Logger.Trace("Insert {id}: {object}", id, obj);

            foreach (RemoteStore store in m_Stores.Values)
            {
                store.SendAdd(id, raw);
            }

            return id;
        }

        public bool Remove(ObjectId id)
        {
            m_PendingAcks.Remove(id);
            bool bRemoved = m_Data.Remove(id);

            foreach (RemoteStore store in m_Stores.Values)
            {
                store.Remove(id);
            }

            return bRemoved;
        }

        public IReadOnlyDictionary<ObjectId, object> Data => m_Data;

        public void AddConnection(ConnectionBase connection)
        {
            if (m_Stores.ContainsKey(connection))
            {
                throw new Exception(
                    $"Cannot create two stores for the same connection {connection}.");
            }

            RemoteStore store = new RemoteStore(m_Data, connection);
            store.OnPacketAddDeserialized += (id, payload, obj) =>
            {
                return RemoteObjectAdded(connection, id, payload, obj);
            };
            store.OnObjectAcknowledged += (id, obj) => { ObjectAcknowledged(connection, id); };
            m_Stores.Add(connection, store);
        }

        private void ObjectAcknowledged(ConnectionBase sender, ObjectId id)
        {
            if (!m_Stores.ContainsKey(sender))
            {
                throw new Exception($"Unknown origin: {sender}.");
            }

            if (!m_PendingAcks.ContainsKey(id)) return;

            PendingResponse pending = m_PendingAcks[id];
            pending.OnAckFrom(m_Stores[sender]);
            if (pending.AllDone())
            {
                pending.Origin.SendACK(id);
                m_PendingAcks.Remove(id);
            }
        }

        private bool RemoteObjectAdded(
            ConnectionBase sender,
            ObjectId id,
            byte[] payload,
            object obj)
        {
            if (!m_Stores.ContainsKey(sender))
            {
                throw new Exception($"Unknown origin: {sender}.");
            }

            List<RemoteStore> otherStores =
                m_Stores.Where(s => s.Key != sender).Select(p => p.Value).ToList();
            if (otherStores.Count == 0)
            {
                // Nothing more to do. Let the store send the ACK.
                return true;
            }

            m_PendingAcks[id] = new PendingResponse(m_Stores[sender], otherStores);
            foreach (RemoteStore store in otherStores)
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
                [NotNull] RemoteStore origin,
                [NotNull] List<RemoteStore> storesToWaitFor)
            {
                Origin = origin;
                m_Pending = storesToWaitFor;
            }

            public RemoteStore Origin { get; }

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
