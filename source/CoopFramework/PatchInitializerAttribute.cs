using System;

namespace CoopFramework
{
    /// <summary>
    ///     Marks a static method to be called once during initialization of all patches.
    ///     The initializer method must have the following signature:
    ///     <code>
    ///     static void Init(ISynchronization sync);
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PatchInitializerAttribute : Attribute
    {
        public bool IsInitialized = false;
    }
}