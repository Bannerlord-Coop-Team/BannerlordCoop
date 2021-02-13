using System;
using System.Collections.Generic;
using Common;

namespace CoopFramework
{
    public static class SynchronizationManager
    {
        public static IReadOnlyList<SynchronizationBase> SynchronizationInstances => m_SynchronizationInstances;
        public static void Register(SynchronizationBase sync)
        {
            m_SynchronizationInstances.Add(sync);
            m_Updatables.Add(sync);
        }
        public static void ProcessBufferedChanges(TimeSpan frameTime)
        {
            m_Updatables.UpdateAll(frameTime);
        }
        private static List<SynchronizationBase> m_SynchronizationInstances = new List<SynchronizationBase>();
        private static UpdateableList m_Updatables = new UpdateableList();
    }
}