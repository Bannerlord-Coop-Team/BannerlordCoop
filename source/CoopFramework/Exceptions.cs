using System;
using Sync.Behaviour;

namespace CoopFramework
{
    /// <summary>
    ///     Thrown when a <see cref="CoopManaged{TSelf,TExtended}"/> instance needs to broadcast something without
    ///     being provided with an <see cref="ISynchronization"/> implementation.
    /// </summary>
    public class SynchronizationNotInitializedException : Exception
    {
        public SynchronizationNotInitializedException(string msg) : base(msg)
        {
        }
    }
}