using System;
using System.Collections.Generic;

namespace Coop.Common
{
    public class UpdateableList
    {
        private readonly object m_Lock = new object();
        private readonly List<IUpdateable> m_Updateables;

        public UpdateableList()
        {
            m_Updateables = new List<IUpdateable>();
        }

        public void UpdateAll(TimeSpan frameTime)
        {
            lock (m_Lock)
            {
                m_Updateables.ForEach(updateable => updateable.Update(frameTime));
            }
        }

        public void Add(IUpdateable updateable)
        {
            lock (m_Lock)
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
            lock (m_Lock)
            {
                m_Updateables.Remove(updateable);
            }
        }
    }
}
