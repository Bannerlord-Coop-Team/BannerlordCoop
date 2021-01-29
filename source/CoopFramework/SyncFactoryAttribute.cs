using System;

namespace CoopFramework
{
    /// <summary>
    ///     Marks a member method as a factory for an <see cref="ISynchronization"/> instance. The factory has a
    ///     signature of:
    ///     (void) -> ISynchronization
    ///
    ///     The factory can be either static or dynamic. The factory will be called on demand by
    ///     <see cref="CoopManaged{TSelf,TExtended}"/>. Depending on the context of the call, either the static
    ///     method will be called or the instance method instance.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class SyncFactoryAttribute : Attribute
    {
    }
}