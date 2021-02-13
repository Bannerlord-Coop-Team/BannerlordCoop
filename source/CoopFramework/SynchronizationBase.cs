using System;
using Common;
using JetBrains.Annotations;
using RemoteAction;
using Sync;

namespace CoopFramework
{
    public abstract class SynchronizationBase : ISynchronization, IUpdateable
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
        public abstract void BroadcastBufferedChanges(FieldChangeBuffer buffer);

        public void Broadcast(FieldChangeBuffer buffer)
        {
            m_Buffer.AddChanges(buffer);
        }
        public void Update(TimeSpan frameTime)
        {
            BroadcastBufferedChanges(m_Buffer);
        }

        [NotNull] private FieldChangeBuffer m_Buffer { get; } = new FieldChangeBuffer();
        
    }
}