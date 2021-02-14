using System;
using Common;
using JetBrains.Annotations;
using RemoteAction;
using Sync;

namespace CoopFramework
{
    /// <summary>
    ///     Base class for synchronization implementations that store changes to fields in a buffer. The 
    ///     The buffer will be synchronized when <seealso cref="SynchronizationManager.ProcessBufferedChanges"/>
    ///     is called, usually once at the end of a game tick.
    /// </summary>
    public abstract class SyncBuffered : ISynchronization, IUpdateable
    {
        #region Debug
        /// <summary>
        ///     Returns the call history of this synchronization instance.
        /// </summary>
        [NotNull] public CallStatistics BroadcastHistory { get; } = new CallStatistics(128);

        #endregion
        /// <inheritdoc cref="ISynchronization.Broadcast(MethodId, object, object[])"/>
        public abstract void Broadcast(MethodId id, object instance, object[] args);
        /// <inheritdoc cref="ISynchronization.Broadcast(FieldChangeBuffer)"/>
        public virtual void Broadcast(FieldChangeBuffer buffer)
        {
            m_Buffer.AddChanges(buffer);
        }
        /// <inheritdoc cref="IUpdateable.Update(TimeSpan)"/>
        public void Update(TimeSpan frameTime)
        {
            if (m_Buffer.Count() > 0)
            {
                BroadcastBufferedChanges(m_Buffer);
            }
        }
        /// <summary>
        ///     Called when the buffered changes should be broadcast. Usually at the end of a game tick.
        /// </summary>
        /// <param name="buffer"></param>
        protected abstract void BroadcastBufferedChanges(FieldChangeBuffer buffer);

        protected SyncBuffered()
        {
            SynchronizationManager.Register(this);
        }
        [NotNull] private FieldChangeBuffer m_Buffer { get; } = new FieldChangeBuffer();
        
    }
}