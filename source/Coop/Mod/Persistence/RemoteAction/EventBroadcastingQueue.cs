﻿using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Connection.Server;
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
        public static readonly int MaximumQueueSize = Globals.DEBUG ? 512 : 8192;

        public bool m_WasFull = false;

        private readonly OrderedHashSet<ObjectId> m_DistributedObjects =
            new OrderedHashSet<ObjectId>();

        private readonly List<Call> m_Queue = new List<Call>();

        [NotNull] private readonly SharedRemoteStore m_Store;

        private readonly TimeSpan m_Timeout;

        private IEnvironmentServer m_EnvironmentServer => CoopServer.Instance.Environment;

        /// <summary>
        /// </summary>
        /// <param name="store">Store to be used for large object transfer.</param>
        /// <param name="eventTimeout">
        ///     Maximum amount of time a single event may spend in the queue. After
        ///     which it is dropped.
        /// </param>
        public EventBroadcastingQueue([NotNull] SharedRemoteStore store, TimeSpan eventTimeout)
        {
            m_Timeout = eventTimeout;
            m_Store = store;
            m_Store.OnObjectDistributed += OnObjectDistributed;
        }

        public int Count
        {
            get
            {
                lock (m_Queue)
                {
                    return m_Queue.Count;
                }
            }
        }

        public void Update(TimeSpan frameTime)
        {
            var numberOfBroadcastEvents = 0;
            lock (m_Queue)
            {
                foreach (var call in m_Queue)
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

                m_Queue.RemoveRange(0, numberOfBroadcastEvents);
                if(m_WasFull && m_Queue.Count == 0)
                {
                    m_WasFull = false;
                    m_EnvironmentServer.UnlockTimeControl();
                }
            }
        }
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
            lock (m_Queue)
            {
                var call = new Call(room, rpc);
                call.ObjectsToBeDistributed.RemoveAll(id => m_DistributedObjects.Contains(id));

                if (m_Queue.Count >= MaximumQueueSize)
                {
                    Logger.Error("Event queue is full!");
                    if (Globals.DEBUG)
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
                }

                m_Queue.Add(call);
                if (m_Queue.Count >= MaximumQueueSize)
                {
                    Logger.Debug("Event queue full, the game is paused to catch up.");
                    m_WasFull = true;
                    m_EnvironmentServer.LockTimeControlStopped();
                }
            }
        }

        private void OnObjectDistributed(ObjectId id)
        {
            lock (m_Queue)
            {
                m_DistributedObjects.Add(id);
                foreach (var call in m_Queue) call.OnObjectDistributed(id);
            }
        }

        private class Call
        {
            public Call(RailServerRoom room, EventActionBase rpc)
            {
                CreatedAt = DateTime.Now;
                Room = room;
                RPC = rpc;

                ObjectsToBeDistributed = RPC.Arguments.Where(arg => arg.StoreObjectId.HasValue)
                    .Select(arg => arg.StoreObjectId.Value).ToList();
            }

            [NotNull] public RailServerRoom Room { get; }

            public EventActionBase RPC { get; }
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
                Logger.Trace("Broadcast: {event}", RPC);
                Room.BroadcastEvent(RPC);
                return true;
            }
        }
    }
}