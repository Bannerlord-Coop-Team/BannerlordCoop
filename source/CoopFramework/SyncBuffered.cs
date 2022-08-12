using System;
using Common;
using Sync.Behaviour;
using Sync.Call;
using Sync.Value;

namespace CoopFramework
{
    /// <summary>
    ///     Base class for synchronization implementations that store changes to fields in a buffer. The
    ///     The buffer will be synchronized when <seealso cref="SSyncBufferManagerProcessBufferedChanges" />
    ///     is called, usually once at the end of a game tick.
    /// </summary>
    public abstract class SyncBuffered : ISynchronization, IUpdateable
    {
        protected SyncBuffered()
        {
            SyncBufferManager.Register(this);
        }

        private FieldChangeBuffer m_Buffer { get; } = new FieldChangeBuffer();

        /// <inheritdoc cref="ISynchronization.Broadcast(InvokableId, object, object[])" />
        public void Broadcast(InvokableId id, object instance, object[] args)
        {
            Broadcast(null, id, instance, args);
        }

        /// <inheritdoc cref="ISynchronization.Broadcast(EntityId[] affectedEntities, InvokableId, object, object[])" />
        public abstract void Broadcast(Guid[] affectedEntities, InvokableId id, object instance, object[] args);

        /// <inheritdoc cref="ISynchronization.Broadcast(FieldChangeBuffer)" />
        public virtual void Broadcast(FieldChangeBuffer buffer)
        {
            m_Buffer.Merge(buffer);
        }

        /// <inheritdoc cref="IUpdateable.Update(TimeSpan)" />
        public void Update(TimeSpan frameTime)
        {
            if (m_Buffer.Count() > 0) BroadcastBufferedChanges(m_Buffer);
        }
        public int Priority { get; } = UpdatePriority.MainLoop.SyncBufferedFields;

        /// <summary>
        ///     Called when the buffered changes should be broadcast. Usually at the end of a game tick.
        /// </summary>
        /// <param name="buffer"></param>
        protected abstract void BroadcastBufferedChanges(FieldChangeBuffer buffer);
    }
}