using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Connection.Server;
using Sync.Store;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Queue to broadcast events. Sends all events in order and makes sure, that all event
    ///     arguments can be resolved on the clients. This includes waiting for any pending
    ///     large object transfers.
    /// </summary>
    public class EventBroadcastingQueue : IUpdateable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        private readonly OrderedHashSet<ObjectId> m_DistributedObjects =
            new OrderedHashSet<ObjectId>();

        private readonly List<Call> m_Queue = new List<Call>();

        [NotNull] private readonly SharedRemoteStore m_Store;

        /// <summary>
        /// </summary>
        /// <param name="store">Store to be used for large object transfer.</param>
        public EventBroadcastingQueue([NotNull] SharedRemoteStore store)
        {
            m_Store = store;
            m_Store.OnObjectDistributed += OnObjectDistributed;
        }

        public void Update(TimeSpan frameTime)
        {
            int numberOfBroadcastEvents = 0;
            lock (m_Queue)
            {
                foreach (Call call in m_Queue)
                {
                    if (!call.TryBroadcast())
                    {
                        TimeSpan timeSinceCreation = DateTime.Now - call.CreatedAt;
                        if (timeSinceCreation > Timeout)
                        {
                            Logger.Error(
                                "{event} has been pending for {timeSinceCreation}. Large object transfer still not completed. Abort event.",
                                call.RPC,
                                timeSinceCreation);
                            call.RPC.Free();
                        }
                        else
                        {
                            // Wait for the next update and try again.
                            break;
                        }
                    }

                    ++numberOfBroadcastEvents;
                }

                m_Queue.RemoveRange(0, numberOfBroadcastEvents);
            }
        }

        /// <summary>
        ///     Adds an event to be broadcast.
        ///     Attention: The queue does not initiate any large object transfers. But it waits
        ///     for the completion of the transfers.
        /// </summary>
        /// <param name="room">Room to send the event to.</param>
        /// <param name="rpc"></param>
        public void Add(RailServerRoom room, EventMethodCall rpc)
        {
            lock (m_Queue)
            {
                Call call = new Call(room, rpc);
                call.ObjectsToBeDistributed.RemoveAll(id => m_DistributedObjects.Contains(id));
                m_Queue.Add(call);
            }
        }

        private void OnObjectDistributed(ObjectId id)
        {
            lock (m_Queue)
            {
                m_DistributedObjects.Add(id);
                foreach (Call call in m_Queue)
                {
                    call.OnObjectDistributed(id);
                }
            }
        }

        private class Call
        {
            public Call(RailServerRoom room, EventMethodCall rpc)
            {
                CreatedAt = DateTime.Now;
                Room = room;
                RPC = rpc;

                ObjectsToBeDistributed = RPC.Call.Arguments.Where(arg => arg.StoreObjectId.HasValue)
                                            .Select(arg => arg.StoreObjectId.Value)
                                            .ToList();
            }

            [NotNull] public RailServerRoom Room { get; }

            public EventMethodCall RPC { get; }
            [NotNull] public List<ObjectId> ObjectsToBeDistributed { get; }

            public DateTime CreatedAt { get; }

            public void OnObjectDistributed(ObjectId id)
            {
                ObjectsToBeDistributed.Remove(id);
            }

            private bool IsReadyToBeSent()
            {
                return ObjectsToBeDistributed.Count == 0;
            }

            public bool TryBroadcast()
            {
                if (!IsReadyToBeSent()) return false;
                Room.BroadcastEvent(RPC);
                Logger.Trace("[{event}] Broadcast", RPC);
                return true;
            }
        }
    }
}
