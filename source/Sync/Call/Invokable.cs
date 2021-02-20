using System.Reflection;
using JetBrains.Annotations;
using Sync.Behaviour;

namespace Sync.Call
{
    /// <summary>
    ///     Type erased invocation wrapper for patched methods. Every invokable instance is registered with
    ///     <see cref="Registry" /> on every client.
    /// </summary>
    public abstract class Invokable
    {
        public Invokable([NotNull] MethodBase original)
        {
            Original = original;
            Id = Registry.Register(this);
        }

        /// <summary>
        ///     Runtime info for the original method. This may or may not be the same method that is being called with
        ///     <see cref="Invoke" />, depending on whether this invokable describes a patch or the original itself.
        /// </summary>
        public MethodBase Original { get; }

        /// <summary>
        ///     Generated id for this invokable. This id is the same on all clients.
        ///     TODO: Currently dictated by order of loading, not actually guaranteed.
        /// </summary>
        public InvokableId Id { get; }

        /// <summary>
        ///     Optional flags relevant for this invokable.
        /// </summary>
        public EInvokableFlag Flags { get; private set; } = EInvokableFlag.None;

        /// <summary>
        ///     Invokes the method call described by the object.
        /// </summary>
        /// <param name="eOrigin">Originator of the call.</param>
        /// <param name="instance">Instance the call is being made on or null for static calls.</param>
        /// <param name="args">Arguments to the call.</param>
        public abstract void Invoke(EOriginator eOrigin, [CanBeNull] object instance, [CanBeNull] object[] args);

        /// <summary>
        ///     Adds a flag.
        /// </summary>
        /// <param name="flag"></param>
        public void AddFlags(EInvokableFlag flag)
        {
            Flags |= flag;
        }
    }
}