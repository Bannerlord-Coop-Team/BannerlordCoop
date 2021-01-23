using System;

namespace CoopFramework
{
    public class SynchronizationNotInitializedException : Exception
    {
        public SynchronizationNotInitializedException(string msg) : base(msg)
        {
        }
    }
}