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
        private readonly List<IUpdateable> m_Updateables = new List<IUpdateable>();

        /// <summary>
        ///     Updates the whole list.
        /// </summary>
        /// <param name="frameTime">Time elapsed since the last call to this function.</param>
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

        /// <summary>
        ///     Adds an entry to the list.
        /// </summary>
        /// <param name="updateable"></param>
        /// <exception cref="ArgumentException"></exception>
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

        /// <summary>
        ///     Removes an entry from the list.
        /// </summary>
        /// <param name="updateable"></param>
        public void Remove(IUpdateable updateable)
        {
            lock (m_Updateables)
            {
                m_Updateables.Remove(updateable);
            }
        }
    }
}
