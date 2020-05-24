using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Sync
{
    public abstract class SyncValue : IWatchable
    {
        /// <summary>
        ///     Returns the current value of an instance of this syncable.
        /// </summary>
        /// <param name="target">Instance.</param>
        /// <returns></returns>
        public abstract object Get(object target);

        private readonly Dictionary<object, Action<object>> m_SyncHandlers =
            new Dictionary<object, Action<object>>();
        [CanBeNull]
        public Action<object> GetSyncHandler([NotNull] object syncableInstance)
        {
            return m_SyncHandlers.TryGetValue(syncableInstance, out Action<object> handler) ?
                handler :
                null;
        }
        public void RemoveSyncHandler([NotNull] object syncableInstance)
        {
             m_SyncHandlers.Remove(syncableInstance);
        }

        /// <summary>
        ///     Sets the current value of an instance of this syncable.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="value"></param>
        public abstract void Set(object target, object value);
        public void SetSyncHandler([NotNull] object syncableInstance, Action<object> action)
        {
            if (m_SyncHandlers.ContainsKey(syncableInstance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_SyncHandlers.Add(syncableInstance, action);
        }
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
