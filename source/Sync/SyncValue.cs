using System;
using System.Collections.Generic;

namespace Sync
{
    public abstract class SyncValue : IWatchable
    {
        private readonly Dictionary<object, Action<object>> m_SyncHandlers =
            new Dictionary<object, Action<object>>();

        public Action<object> GetSyncHandler(object syncableInstance)
        {
            return m_SyncHandlers.TryGetValue(syncableInstance, out Action<object> handler) ?
                handler :
                null;
        }

        public void RemoveSyncHandler(object syncableInstance)
        {
            m_SyncHandlers.Remove(syncableInstance);
        }

        public void SetSyncHandler(object syncableInstance, Action<object> action)
        {
            if (m_SyncHandlers.ContainsKey(syncableInstance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_SyncHandlers.Add(syncableInstance, action);
        }

        /// <summary>
        ///     Returns the current value of an instance of this syncable.
        /// </summary>
        /// <param name="target">Instance.</param>
        /// <returns></returns>
        public abstract object Get(object target);

        /// <summary>
        ///     Sets the current value of an instance of this syncable.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="value"></param>
        public abstract void Set(object target, object value);
    }

    public static class SyncableInstance
    {
        /// <summary>
        ///     Reserved value for a ISyncable::SyncHandler that gets called
        ///     when any instance is changed.
        /// </summary>
        public static object Any { get; } = new object();
    }
}
