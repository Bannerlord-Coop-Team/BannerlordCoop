using System;
using System.Collections.Generic;
using Common;

namespace CoopFramework
{
    /// <summary>
    ///     Manager for instances of <see cref="SyncBuffered"/>. Used to process the buffered changes of all registered
    ///     instances.
    /// </summary>
    public static class SyncBufferManager
    {
        private static readonly List<SyncBuffered> m_SynchronizationInstances = new List<SyncBuffered>();
        public static UpdateableList ProcessBufferedChanges { get; }= new UpdateableList();
        public static IReadOnlyList<SyncBuffered> SynchronizationInstances => m_SynchronizationInstances;

        /// <summary>
        ///     Registers an instance.
        /// </summary>
        /// <param name="sync"></param>
        public static void Register(SyncBuffered sync)
        {
            m_SynchronizationInstances.Add(sync);
            ProcessBufferedChanges.Add(sync);
        }
    }
}