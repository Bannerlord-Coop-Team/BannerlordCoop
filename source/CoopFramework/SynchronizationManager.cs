using System.Collections.Generic;

namespace CoopFramework
{
    public static class SynchronizationManager
    {
        public static IReadOnlyList<SynchronizationBase> SynchronizationInstances => m_SynchronizationInstances;
        public static void Register(SynchronizationBase sync)
        {
            m_SynchronizationInstances.Add(sync);
        }

        private static List<SynchronizationBase> m_SynchronizationInstances = new List<SynchronizationBase>();
    }
}