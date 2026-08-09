using Common.Logging;
using Common.Util;
using Serilog;
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
        // Resolved per call rather than held in a static field, the same reason BootPatches uses a
        // property: CoopMod's static Updateables list is built while the runtime initializes CoopMod,
        // which happens on entry to NoHarmonyInit — before its first statement installs the mod's
        // assembly binding redirects. A static field here would build the Serilog graph at that moment,
        // and the FileNotFoundException it throws surfaces as a TypeInitializationException that kills
        // the process during module load. Only the fault path below logs, so the cost is irrelevant.
        private static ILogger Logger => LogManager.GetLogger<UpdateableList>();

        private List<IUpdateable> m_UpdateablesSorted = new List<IUpdateable>();

        // A faulting entry stays in the list, so without throttling a permanent fault would log a
        // stack trace every frame. Same reasoning as Poller's loop.
        private readonly FaultLogThrottle faultThrottle = new FaultLogThrottle();

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
                // The engine's tick drives this list, so an escaping exception ends the process and skips
                // the entries behind it.
                try
                {
                    updateable.Update(frameTime);
                }
                catch (Exception e)
                {
                    string name = updateable.GetType().Name;
                    switch (faultThrottle.Classify(name, e, out long repeats))
                    {
                        case FaultLogAction.Full:
                            Logger.Error(e, "{Updateable} threw during update and was suppressed", name);
                            break;
                        case FaultLogAction.Summary:
                            Logger.Error("{Updateable} still throwing the same exception ({RepeatCount}x): {Message}",
                                name, repeats, e.Message);
                            break;
                    }
                }
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
