using Extensions.Data;
using JetBrains.Annotations;
using Network.Infrastructure;
using Network.Protocol;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sync.Store
{
    /// <summary>
    ///     Server side implementation of a shared object storage.
    ///     
    ///     This storage is connected to all <see cref="RemoteStoreClient"/>. It will broadcast
    ///     all changes to the connected clients.
    ///     
    ///     IMPORTANT: The user of this store is responsible to remove objects once they are no longer
    ///     needed! When you <see cref="Insert(object)"/> an object into the store, you need to call
    ///     <see cref="Remove(ObjectId)"/> in the same game instance to remove it.
    /// </summary>
    public class RemoteStoreServer : IStore
    {
        /// <summary>
        ///     State for a shared object managed by the store.
        /// </summary>
        public class SharedObject
        {
            /// <summary>
            ///     State of the object for each remote store instance.
            /// </summary>
            public Dictionary<ConnectionBase, State> RemoteState { get; } = new Dictionary<ConnectionBase, State>();

            /// <summary>
            ///     Incremented by 1 when Insert is called for this object on the server side.
            /// </summary>
            public uint InsertCountServer { get; set; } = 0;

            /// <summary>
            ///     Incremented by 1 when <see cref="IStore.Retrieve(ObjectId)"/> is called for this object on the server side.
            /// </summary>
            public uint RetrieveCountServer { get; set; } = 0;

            /// <summary>
            ///     Returns true if the object is available in all currently connect store instaces.
            /// </summary>
            /// <returns></returns>
            public bool IsAvailableEverywhere()
            {
                return RemoteState.All(p => p.Value.DidSendInsertAck || p.Value.InsertCount > 0); 
            }

            public class State
            {
                /// <summary>
                ///     True if a StoreInsertAck was sent by the remote instance for this object.
                /// </summary>
                public bool DidSendInsertAck { get; set;} = false;

                /// <summary>
                ///     Incremented by 1 when Insert is called with this object by the remote instance.
                /// </summary>
                public uint InsertCount {  get; set;} = 0;

                /// <summary>
                ///     Incremented by 1 when the remote instance calls <see cref="IStore.Retrieve(ObjectId)"/>.
                /// </summary>
                public uint RetrieveCount { get; set;} = 0;
            }
        }

        /// <summary>
        ///     Triggered when an object that has been added through <see cref="Insert"/> is available
        ///     on all store instances.
        /// </summary>
        public Action<ObjectId> OnObjectAvailable;

        /// <summary>
        ///     Triggered when an instance called <see cref="IStore.Retrieve(ObjectId)"/> for an object,
        ///     including when the server retrieves.
        ///     If the callback returns true, the object is permanently removed from the store.
        /// </summary>
        public Func<ObjectId, SharedObject, bool> OnObjectRetrieved;
        public RemoteStoreServer([NotNull] ISerializableFactory serializableFactory)
        {
            m_Serializer = new StoreSerializer(serializableFactory);
        }
        public void Dispose()
        {
            foreach(ConnectionBase connection in m_Connections)
            {
                connection.Dispatcher.UnregisterPacketHandlers(this);
            }
        }

        /// <summary>
        ///     Connects an additional remote store.
        /// </summary>
        /// <param name="connection"></param>
        public void AddConnection(ConnectionBase connection)
        {
            connection.Dispatcher.RegisterPacketHandler(ReceiveInsert);
            connection.Dispatcher.RegisterPacketHandler(ReceiveStoreInsertAck);
            connection.Dispatcher.RegisterPacketHandler(ReceiveStoreRemove);
            connection.Dispatcher.RegisterPacketHandler(ReceiveStoreDataRetrieved);
            m_Connections.Add(connection);
        }
        
        /// <summary>
        ///     Disconnects a remote store
        /// </summary>
        /// <param name="connection"></param>
        public void RemoveConnection(ConnectionBase connection)
        {
            connection.Dispatcher.UnregisterPacketHandlers(this);
            m_Connections.Remove(connection);
        }
        public IReadOnlyDictionary<ObjectId, SharedObject> State => m_State;

        #region IStore
        /// <summary>
        ///     Serialize an object.
        /// </summary>
        /// <param name="obj">Object to serialize.</param>
        /// <returns></returns>
        public byte[] Serialize(object obj)
        {
            return m_Serializer.Serialize(obj);
        }

        /// <summary>
        ///     Deserialize an object.
        /// </summary>
        /// <param name="obj">Object to deserialize.</param>
        /// <returns></returns>
        public object Deserialize(byte[] raw)
        {
            return m_Serializer.Deserialize(raw);
        }

        /// <summary>
        ///     Inserts an object into the store. Once the object is available in all currently connected
        ///     remote stores <see cref="OnObjectAvailable"/> is triggered.
        ///     
        ///     If the object is already distributed to all connected stores, <see cref="OnObjectAvailable"/>
        ///     is called immediatly. 
        /// </summary>
        /// <param name="obj">The object to insert.</param>
        /// <returns>Id of the inserted object.</returns>
        public ObjectId Insert(object obj)
        {
            return Insert(obj, Serialize(obj));
        }

        /// <summary>
        ///     Inserts an object into the store. Once the object is available in all currently connected
        ///     remote stores <see cref="OnObjectAvailable"/> is triggered.
        ///     
        ///     If the object is already distributed to all connected stores, <see cref="OnObjectAvailable"/>
        ///     is called immediatly.
        /// </summary>
        /// <param name="obj">The object to insert.</param>
        /// <param name="serialized">Serialized representation of obj."/></param>
        /// <returns>Id of the inserted object.</returns>
        public ObjectId Insert(object obj, byte[] serialized)
        {
            var id = new ObjectId(XXHash.XXH32(serialized));
            if (!m_State.TryGetValue(id, out SharedObject shared))
            {
                // New object
                m_Data[id] = obj;
                shared = new SharedObject { InsertCountServer = 1};
                m_State.Add(id, shared);
                foreach(ConnectionBase connection in m_Connections)
                {
                    connection.Send(new Packet(EPacket.StoreInsert, serialized));
                    shared.RemoteState.Add(connection, new SharedObject.State());
                }
            }
            else
            {
                shared.InsertCountServer++;
                if (shared.IsAvailableEverywhere())
                {
                    OnObjectAvailable?.Invoke(id);
                }
            }
            return id;
        }

        /// <summary>
        ///     Access an object.
        /// </summary>
        /// <param name="id"></param>
        [CanBeNull]
        public object Retrieve(ObjectId id)
        {
            if (!m_Data.TryGetValue(id, out object data))
            {
                return null;
            }

            if (OnObjectRetrieved != null)
            {
                m_State[id].RetrieveCountServer++;
                bool doRemove = OnObjectRetrieved(id, m_State[id]);
                if (doRemove)
                {
                    foreach (ConnectionBase connection in m_Connections)
                    {
                        connection.Send(new Packet(EPacket.StoreRemove, id));
                    }
                    m_State.Remove(id);
                    m_Data.Remove(id);
                }
            }
            
            return data;
        }
        #endregion
        public IReadOnlyDictionary<ObjectId, object> Data => m_Data;

        #region PacketHandlers

        [ConnectionClientPacketHandler(EClientConnectionState.Connected, EPacket.StoreInsert)]
        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.StoreInsert)]
        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.StoreInsert)]
        private void ReceiveInsert(ConnectionBase sender, Packet packet)
        {
            // Receive the object
            var raw = packet.Payload.ToArray();
            var id = new ObjectId(XXHash.XXH32(raw));

            // Add it if not already present
            if (!m_State.TryGetValue(id, out SharedObject shared))
            {
                shared = new SharedObject{};
                m_State.Add(id, shared);
            }
            if (!m_Data.TryGetValue(id, out object obj))
            {
                obj = Deserialize(raw);
                m_Data.Add(id, obj);
            }

            // Forward the add to all other connections
            foreach (ConnectionBase connection in m_Connections)
            {
                // Update state object
                if (!shared.RemoteState.TryGetValue(connection, out SharedObject.State state))
                {
                    state = new SharedObject.State();
                    shared.RemoteState.Add(connection, state);
                }

                if(connection == sender)
                {
                    state.InsertCount++;
                }
                else
                {
                    state.DidSendInsertAck = false;
                    connection.Send(new Packet(EPacket.StoreInsert, raw));
                }
            }

            if(shared.IsAvailableEverywhere())
            {
                OnObjectAvailable?.Invoke(id);
            }
        }

        [ConnectionClientPacketHandler(EClientConnectionState.Connected, EPacket.StoreInsertAck)]
        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.StoreInsertAck)]
        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.StoreInsertAck)]
        private void ReceiveStoreInsertAck(ConnectionBase connection, Packet packet)
        {
            var id = new ObjectId(packet.Payload.ToArray());
            if (!m_Data.ContainsKey(id) || !m_State.TryGetValue(id, out SharedObject shared))
            {
                Logger.Error($"Received StoreInsertAck for unknown object {id}. Ignored.");
                return;
            }

            if (!shared.RemoteState.TryGetValue(connection, out SharedObject.State remoteState))
            {
                Logger.Error($"Received StoreInsertAck for object {id} from store that was not sent the object. This is a bug.");
                return;
            }

            remoteState.DidSendInsertAck = true;
            if (shared.IsAvailableEverywhere())
            {
                OnObjectAvailable?.Invoke(id);
            }
        }

        [ConnectionClientPacketHandler(EClientConnectionState.Connected, EPacket.StoreDataRetrieved)]
        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.StoreDataRetrieved)]
        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.StoreDataRetrieved)]
        private void ReceiveStoreDataRetrieved(ConnectionBase sender, Packet packet)
        {
            var id = new ObjectId(packet.Payload.ToArray());
            if (!m_Data.ContainsKey(id) || !m_State.TryGetValue(id, out SharedObject shared))
            {
                Logger.Error($"Received StoreAccess for unknown object {id}. Ignored.");
                return;
            }

            if (!shared.RemoteState.TryGetValue(sender, out SharedObject.State remoteState))
            {
                Logger.Error($"Received StoreAccess for object {id} from store that was not sent the object. This is a bug.");
                return;
            }

            remoteState.RetrieveCount++;
            if (OnObjectRetrieved != null)
            {
                bool doRemove = OnObjectRetrieved.Invoke(id, shared);
                if (doRemove)
                {
                    foreach (ConnectionBase connection in m_Connections)
                    {
                        connection.Send(new Packet(EPacket.StoreRemove, id));
                    }
                    m_State.Remove(id);
                    m_Data.Remove(id);
                }
            }
        }

        [ConnectionClientPacketHandler(EClientConnectionState.Connected, EPacket.StoreRemove)]
        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.StoreRemove)]
        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.StoreRemove)]
        private void ReceiveStoreRemove(ConnectionBase sender, Packet packet)
        {
            Logger.Error($"Received StoreRemove from a client. Only the server may remove. Ignored.");
        }

        #endregion

        #region Internals
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<ObjectId, object> m_Data = new Dictionary<ObjectId, object>();
        private readonly StoreSerializer m_Serializer;
        private readonly Dictionary<ObjectId, SharedObject> m_State = new Dictionary<ObjectId, SharedObject>();
        private readonly List<ConnectionBase> m_Connections = new List<ConnectionBase>();
        #endregion
    }
}
