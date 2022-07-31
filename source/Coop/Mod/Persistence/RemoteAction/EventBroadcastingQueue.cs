using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Connection;
using RailgunNet.Connection.Server;
using RailgunNet.System.Types;
using Sync.Store;

namespace Coop.Mod.Persistence.RemoteAction
{
    /// <summary>
    ///     Queue to broadcast events. Sends all events in order and makes sure, that all event
    ///     arguments can be resolved on the clients. This includes waiting for any pending
    ///     large object transfers.
    /// </summary>
    public class EventBroadcastingQueue : IUpdateable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Defines the maximum number of events that can be added to the queue. Smaller in
        ///     DEBUG to identify situations where events are starving in the queue. Might need
        ///     to be adjusted depending on how much is done using events.
        /// </summary>
#if DEBUG
        public static readonly int MaximumQueueSize = 2048;
#else
        public static readonly int MaximumQueueSize = 16384;
#endif

        [NotNull] private readonly RemoteStoreServer m_Store;

        /// <summary>
        /// </summary>
        /// <param name="store">Store to be used for large object transfer.</param>
        /// <param name="eventTimeout">
        ///     Maximum amount of time a single event may spend in the queue. After
        ///     which it is dropped.
        /// </param>
        public EventBroadcastingQueue([NotNull] RemoteStoreServer store, TimeSpan eventTimeout)
        {
            m_Timeout = eventTimeout;
            m_Store = store;
            m_Store.OnObjectAvailable += OnObjectAvailable;
        }

        /// <summary>
        ///     Returns the number of pending events in the queue.
        /// </summary>
        public int Count
        {
            get
            {
                lock (m_QueuePending)
                {
                    return m_QueuePending.Count;
                }
            }
        }

        /// <summary>
        ///     Checks all pending calls. If ready, they are broadcast.
        /// </summary>
        /// <param name="frameTime"></param>
        public void Update(TimeSpan frameTime)
        {
            List<Call> finishedCalls = BroadcastPendingEvents();

            // Remove objects from m_Store that are no longer needed.
            List<ObjectId> candidatesForRemove = new List<ObjectId>();
            lock (m_QueueCleanup)
            {
                m_QueueCleanup = m_QueueCleanup
                    .Union(finishedCalls.Where(c => c.ObjectsNeededForCall.Any()))
                    .Where(call =>
                {
                    // We want to keep calls in the cleanup list that have not yet been executed on all clients.
                    //foreach (RailPeer client in call.Room.Clients)
                    //{
                    //    // Check the event ID of the events we sent against the last event processed by that peer.
                    //    if (call.BroadcastEvents.TryGetValue(client, out SequenceId eventId))
                    //    {
                    //        if (client.LastAckEventId < eventId)
                    //        {
                    //            // That client has not yet processed the event => keep the call for future cleanup
                    //            return true;
                    //        }
                    //    }
                    //}

                    // All clients have received and processed this call. The arguments are no longer needed.
                    foreach (ObjectId id in call.ObjectsNeededForCall)
                    {
                        candidatesForRemove.Add(id);
                    }
                    return false;
                }).ToList();
            }

            if(candidatesForRemove.Count > 0)
            {
                lock (m_QueuePending)
                {
                    foreach (ObjectId id in candidatesForRemove)
                    {
                        // Before removing it, check if any of the pending RPC use the same object. That saves
                        // us an unnecessary insert.
                        if(!m_QueuePending.Any(c => c.ObjectsNeededForCall.Contains(id)))
                        {
                            m_Store.Remove(id);
                        }
                    }
                }
            }
        }        

        /// <summary>
        ///     Returns the update priority for this queue.
        /// </summary>
        public int Priority { get; } = UpdatePriority.ServerThread.ProcessBroadcasts;

        /// <summary>
        ///     Adds an event to be broadcast.
        ///     Attention: The queue does not initiate any large object transfers. But it waits
        ///     for the completion of the transfers.
        /// </summary>
        /// <param name="room">Room to send the event to.</param>
        /// <param name="rpc"></param>
        public void Add(RailServerRoom room, EventActionBase rpc)
        {
            lock (m_QueuePending)
            {
                var call = new Call(room, rpc);
                call.WaitingForObjects.RemoveAll(id => m_DistributedObjects.Contains(id));

                if (m_QueuePending.Count >= MaximumQueueSize)
                {
                    Logger.Error("Event queue is full!");
#if DEBUG
                    // Events seem to starve in the queue. This indicates an underlying issue.
                    // Do one of the following:
                    // 1. Did you change anything that increases the number of generated events
                    //    in a single frame?
                    //    yes -> Increase MaximumQueueSize accordingly.
                    // 2. Did you introduce an event with very large payloads that is blocking
                    //    the queue while other events are occuring?
                    //    yes -> Maybe the game should be be paused while we that event is
                    //           transferred? Remember that the queue is always executed in
                    //           sequence to guarantee a consistent state.
                    // 3. Open a bug.
                    throw new IndexOutOfRangeException();
#endif
                }

                m_QueuePending.Add(call);
            }
        }

        #region Internals
        /// <summary>
        ///     Checks all pending calls. If ready, they are broadcast.
        /// </summary>
        private List<Call> BroadcastPendingEvents()
        {
            lock (m_QueuePending)
            {
                var numberOfBroadcastEvents = 0;
                foreach (var call in m_QueuePending)
                {
                    if (!call.TryBroadcast())
                    {
                        var timeSinceCreation = DateTime.Now - call.CreatedAt;
                        if (timeSinceCreation > m_Timeout)
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
                List<Call> finishedCalls = m_QueuePending.Take(numberOfBroadcastEvents).ToList();
                m_QueuePending.RemoveRange(0, numberOfBroadcastEvents);
                return finishedCalls;
            }
        }

        /// <summary>
        ///     Called by the store when an object is available on all instances.
        /// </summary>
        /// <param name="id"></param>
        private void OnObjectAvailable(ObjectId id)
        {
            lock (m_QueuePending)
            {
                m_DistributedObjects.Add(id);
                foreach (var call in m_QueuePending) call.OnObjectDistributed(id);
            }
        }

        /// <summary>
        ///     Represents a pending RPC.
        /// </summary>
        private class Call
        {
            public Call(RailServerRoom room, EventActionBase rpc)
            {
                CreatedAt = DateTime.Now;
                Room = room;
                RPC = rpc;

                WaitingForObjects = RPC.Arguments.Where(arg => arg.StoreObjectId.HasValue)
                    .Select(arg => arg.StoreObjectId.Value).ToList();
                ObjectsNeededForCall = WaitingForObjects.ToList();
            }

            [NotNull] public RailServerRoom Room { get; }

            public EventActionBase RPC { get; }
            [NotNull] public List<ObjectId> WaitingForObjects { get; }
            [NotNull] public List<ObjectId> ObjectsNeededForCall { get; }

            public DateTime CreatedAt { get; }

            public void OnObjectDistributed(ObjectId id)
            {
                WaitingForObjects.Remove(id);
            }

            private bool IsReadyToBeSent()
            {
                return WaitingForObjects.Count == 0;
            }

            public bool TryBroadcast()
            {
                if (!IsReadyToBeSent())
                { 
                    return false;
                }
                Room.BroadcastEvent(RPC);
                return true;
            }
        }

        private readonly OrderedHashSet<ObjectId> m_DistributedObjects = new OrderedHashSet<ObjectId>();

        private readonly List<Call> m_QueuePending = new List<Call>();
        private List<Call> m_QueueCleanup = new List<Call>();
        private readonly TimeSpan m_Timeout;

        #endregion
    }
}