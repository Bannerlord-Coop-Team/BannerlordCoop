using System;

namespace CoopFramework
{
    /// <summary>
    ///     Marks a static method to be called once during initialization of all patches.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PatchInitializerAttribute : Attribute
    {
    }
}
