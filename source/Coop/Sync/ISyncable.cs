using System;
using JetBrains.Annotations;

namespace Coop.Sync
{
    public interface ISyncable
    {
        /// <summary>
        ///     Sets the handler to be called when a specific instance of the syncable
        ///     requested a change. Multiple instance specific handlers are not supported.
        /// </summary>
        /// <param name="syncableInstance"></param>
        /// <param name="action"></param>
        void SetSyncHandler([NotNull] object syncableInstance, Action<object> action);

        void RemoveSyncHandler([NotNull] object syncableInstance);

        [CanBeNull]
        Action<object> GetSyncHandler([NotNull] object syncableInstance);

        /// <summary>
        ///     Returns the current value of an instance of this syncable.
        /// </summary>
        /// <param name="target">Instance.</param>
        /// <returns></returns>
        object Get(object target);

        /// <summary>
        ///     Sets the current value of an instance of this syncable.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="value"></param>
        void Set(object target, object value);
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
