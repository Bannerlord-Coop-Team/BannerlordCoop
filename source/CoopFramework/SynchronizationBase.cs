using JetBrains.Annotations;
using RemoteAction;
using Sync;

namespace CoopFramework
{
    public abstract class SynchronizationBase : ISynchronization
    {
        #region Debug
        /// <summary>
        ///     Returns the call history of this synchronization instance.
        /// </summary>
        [NotNull] public CallStatistics BroadcastHistory { get; } = new CallStatistics(128);

        #endregion

        protected SynchronizationBase()
        {
            SynchronizationManager.Register(this);
        }

        public abstract void Broadcast(MethodId id, object instance, object[] args);

        public abstract void Broadcast(FieldChangeBuffer buffer);
    }
}