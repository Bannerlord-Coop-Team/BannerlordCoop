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
    /// <summary>
    ///     Client side implementation of a shared object storage.
    ///     
    ///     This storage is connected to a <see cref="RemoteStoreServer"/>. All inserts and removes
    ///     in this storage will be sent to the server as well. The server will forward changes to 
    ///     all other connected instances.
    ///     
    ///     IMPORTANT: The user of this store is responsible to remove objects once they are no longer
    ///     needed! When you <see cref="Insert(object)"/> an object into the store, you need to call
    ///     <see cref="Remove(ObjectId)"/> in the same game instance to remove it.
    /// </summary>
    public class RemoteStoreClient : IStore, IDisposable
    {
        /// <summary>
        ///     Creates a new store.
        /// </summary>
        /// <param name="connection">connection to be used to communicate with the remote store</param>
        public RemoteStoreClient(
            [NotNull] ConnectionBase connection,
            [NotNull] ISerializableFactory serializableFactory)
        {
            m_Serializer = new StoreSerializer(serializableFactory);
            m_Connection = connection;
            m_Connection.Dispatcher.RegisterPacketHandler(ReceiveInsert);
            m_Connection.Dispatcher.RegisterPacketHandler(ReceiveRemove);
        }

        #region IStore
        public byte[] Serialize(object obj)
        {
            return m_Serializer.Serialize(obj);
        }

        public object Deserialize(byte[] raw)
        {
            return m_Serializer.Deserialize(raw);
        }

        /// <summary>
        ///     Inserts an object into the store. Call <see cref="Remove"/> when 
        ///     the object is no longer needed in the store.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public ObjectId Insert(object obj)
        {
            return Insert(obj, Serialize(obj));
        }

        /// <summary>
        ///     Inserts an object into the store. Call <see cref="Remove"/> when 
        ///     the object is no longer needed in the store.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public ObjectId Insert(object obj, byte[] serialized)
        {
            var id = new ObjectId(XXHash.XXH32(serialized));
            if (!m_Data.TryGetValue(id, out object data))
            {
                data = obj;
                m_Data.Add(id, data);
            }

            m_Connection.Send(new Packet(EPacket.StoreInsert, serialized));
            return id;
        }

        /// <summary>
        ///     Access an object. Be aware that this is communicated to the server. The server
        ///     may decide to remove the object after the client has accessed it.
        /// </summary>
        /// <param name="id"></param>
        [CanBeNull] public object Retrieve(ObjectId id)
        {
            if (!m_Data.TryGetValue(id, out object data))
            {
                return null;
            }

            m_Connection.Send(new Packet(EPacket.StoreDataRetrieved, id));
            return data;
        }
        public IReadOnlyDictionary<ObjectId, object> Data => m_Data;
        #endregion

        public void Dispose()
        {
            m_Connection.Dispatcher.UnregisterPacketHandlers(this);
        }

        #region PacketHandlers

        [ConnectionClientPacketHandler(EClientConnectionState.Connected, EPacket.StoreInsert)]
        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.StoreInsert)]
        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.StoreInsert)]
        private void ReceiveInsert(ConnectionBase connection, Packet packet)
        {
            // Receive the object
            var raw = packet.Payload.ToArray();
            var id = new ObjectId(XXHash.XXH32(raw));
            if (!m_Data.ContainsKey(id))
            {
                m_Data.Add(id, Deserialize(raw));
            }

            m_Connection.Send(new Packet(EPacket.StoreInsertAck, id));
        }

        [ConnectionClientPacketHandler(EClientConnectionState.Connected, EPacket.StoreRemove)]
        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.StoreRemove)]
        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.StoreRemove)]
        private void ReceiveRemove(ConnectionBase connection, Packet packet)
        {
            var id = new ObjectId(packet.Payload.ToArray());
            if (!m_Data.TryGetValue(id, out object data))
            {
                Logger.Error($"Received StoreRemoveRequestCommit for unknown object {id}. Ignored.");
                return;
            }
            m_Data.Remove(id);
        }
        #endregion

        #region Internals
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConnectionBase m_Connection;
        private readonly Dictionary<ObjectId, object> m_Data = new Dictionary<ObjectId, object>();
        private readonly StoreSerializer m_Serializer;
        #endregion
    }
}