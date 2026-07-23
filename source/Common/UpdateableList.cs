using System;
using System.Collections.Generic;
using System.Linq;

namespace Common
{
    /// <summary>
    ///     Manages a list of <see cref="IUpdateable"/>.
    /// </summary>
    public class UpdateableList
    {
        private List<IUpdateable> m_UpdateablesSorted = new List<IUpdateable>();

        /// <summary>
        ///     Updates the whole list.
        /// </summary>
        /// <param name="frameTime">Time elapsed since the last call to this function.</param>
        public void UpdateAll(TimeSpan frameTime)
        {
            List<IUpdateable> iterationCopy;
            lock (m_UpdateablesSorted)
            {
                iterationCopy = new List<IUpdateable>(m_UpdateablesSorted);
            }

            foreach (IUpdateable updateable in iterationCopy)
            {
                updateable.Update(frameTime);
            }
        }

        /// <summary>
        ///     Adds an entry to the list.
        /// </summary>
        /// <param name="updateable"></param>
        /// <exception cref="ArgumentException"></exception>
        public void Add(IUpdateable updateable)
        {
            lock (m_UpdateablesSorted)
            {
                if (m_UpdateablesSorted.Contains(updateable))
                {
                    throw new ArgumentException($"duplicate entry for {updateable}.");
                }
                
                m_UpdateablesSorted.Add(updateable);
                m_UpdateablesSorted = m_UpdateablesSorted.OrderBy(o => o.Priority).Reverse().ToList();
            }
        }

        /// <summary>
        ///     Removes an entry from the list.
        /// </summary>
        /// <param name="updateable"></param>
        public void Remove(IUpdateable updateable)
        {
            lock (m_UpdateablesSorted)
            {
                m_UpdateablesSorted.Remove(updateable);
            }
        }
        /// <summary>
        ///     Creates a new list containing the union of this list and the given list.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public UpdateableList MakeUnion(UpdateableList other)
        {
            UpdateableList union = new UpdateableList();
            lock (m_UpdateablesSorted)
            {
                lock (other.m_UpdateablesSorted)
                {
                    union.m_UpdateablesSorted.AddRange(m_UpdateablesSorted);
                    union.m_UpdateablesSorted.AddRange(other.m_UpdateablesSorted);
                    union.m_UpdateablesSorted = union.m_UpdateablesSorted
                        .Distinct()
                        .OrderBy(o => o.Priority)
                        .Reverse()
                        .ToList();
                }
            }

            return union;
        }
    }
}
