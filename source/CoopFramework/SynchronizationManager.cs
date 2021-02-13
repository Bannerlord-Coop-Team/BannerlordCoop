using System;
using System.Collections.Generic;
using Common;

namespace CoopFramework
{
    public static class SynchronizationManager
    {
        public static IReadOnlyList<SyncBuffered> SynchronizationInstances => m_SynchronizationInstances;
        public static void Register(SyncBuffered sync)
        {
            m_SynchronizationInstances.Add(sync);
            m_Updatables.Add(sync);
        }
        public static void ProcessBufferedChanges(TimeSpan frameTime)
        {
            m_Updatables.UpdateAll(frameTime);
        }
        private static List<SyncBuffered> m_SynchronizationInstances = new List<SyncBuffered>();
        private static UpdateableList m_Updatables = new UpdateableList();
    }
}