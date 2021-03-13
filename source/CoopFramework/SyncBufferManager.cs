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
        private static readonly UpdateableList m_Updatables = new UpdateableList();
        public static IReadOnlyList<SyncBuffered> SynchronizationInstances => m_SynchronizationInstances;

        /// <summary>
        ///     Registers an instance.
        /// </summary>
        /// <param name="sync"></param>
        public static void Register(SyncBuffered sync)
        {
            m_SynchronizationInstances.Add(sync);
            m_Updatables.Add(sync);
        }

        /// <summary>
        ///     Processes all buffered changes.
        /// </summary>
        /// <param name="frameTime"></param>
        public static void ProcessBufferedChanges(TimeSpan frameTime)
        {
            m_Updatables.UpdateAll(frameTime);
        }
    }
}