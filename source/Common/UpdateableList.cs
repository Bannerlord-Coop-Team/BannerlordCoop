using System;
using System.Collections.Generic;

namespace Common
{
    public class UpdateableList
    {
        private readonly List<IUpdateable> m_Updateables = new List<IUpdateable>();

        public void UpdateAll(TimeSpan frameTime)
        {
            List<IUpdateable> iterationCopy;
            lock (m_Updateables)
            {
                iterationCopy = new List<IUpdateable>(m_Updateables);
            }

            foreach (IUpdateable updateable in iterationCopy)
            {
                updateable.Update(frameTime);
            }
        }

        public void Add(IUpdateable updateable)
        {
            lock (m_Updateables)
            {
                if (m_Updateables.Contains(updateable))
                {
                    throw new ArgumentException($"duplicate entry for {updateable}.");
                }

                m_Updateables.Add(updateable);
            }
        }

        public void Remove(IUpdateable updateable)
        {
            lock (m_Updateables)
            {
                m_Updateables.Remove(updateable);
            }
        }
    }
}
