using System;
using JetBrains.Annotations;

namespace Sync
{
    public interface IWatchable
    {
        /// <summary>
        ///     Sets the handler to be called when a specific instance of the syncable
        ///     requested a change. Multiple instance specific handlers are not supported.
        ///     The argument passed to the action are the arguments, not the instance!
        /// </summary>
        /// <param name="syncableInstance"></param>
        /// <param name="action"></param>
        void SetSyncHandler([NotNull] object syncableInstance, Action<object> action);

        void RemoveSyncHandler([NotNull] object syncableInstance);

        [CanBeNull]
        Action<object> GetSyncHandler([NotNull] object syncableInstance);
    }
}
