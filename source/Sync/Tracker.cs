using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Sync
{
    /// <summary>
    ///     Base class for class wrappers that notify when specific instances of the wrapped class
    ///     change internal state.
    /// </summary>
    public abstract class Tracker
    {
        private readonly Dictionary<object, Func<object, bool>> m_InstanceSpecificHandlers =
            new Dictionary<object, Func<object, bool>>();

        public Func<object, object, bool> GlobalHandler { get; private set; }

        public IReadOnlyDictionary<object, Func<object, bool>> InstanceSpecificHandlers =>
            m_InstanceSpecificHandlers;

        /// <summary>
        ///     Sets the handler to be called when a specific instance of the <see cref="Tracker" />
        ///     requested a change. Multiple instance specific handlers are not supported.
        ///     The argument passed to the action are the arguments, not the instance!
        ///
        ///     The return value of the handler decides if the unpatched methods should be called (true)
        ///     or not (false). Note that in the case of multiple patches for one method, the call order
        ///     is inverse to the patching order (last patch gets called first). If the first patch returns
        ///     true, the next patch will be called and so on.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="handler"></param>
        public void SetHandler([NotNull] object instance, [NotNull] Func<object, bool> handler)
        {
            if (m_InstanceSpecificHandlers.ContainsKey(instance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_InstanceSpecificHandlers.Add(instance, handler);
        }

        /// <summary>
        ///     Gets the handler to be called when the given instance changes.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns>Handler or null</returns>
        public Func<object, bool> GetHandler(object instance)
        {
            bool bHasGlobalHandler = GlobalHandler != null;
            if (instance != null &&
                m_InstanceSpecificHandlers.TryGetValue(
                    instance,
                    out Func<object, bool> instanceSpecificHandler))
            {
                if (bHasGlobalHandler)
                {
                    return args =>
                    {
                        GlobalHandler(instance, args);
                        return instanceSpecificHandler(args);
                    };
                }

                return instanceSpecificHandler;
            }

            if (GlobalHandler != null)
            {
                return args => GlobalHandler(instance, args);
            }

            return null;
        }

        /// <summary>
        ///     Removes an instance specific handler.
        /// </summary>
        /// <param name="instance"></param>
        public void RemoveHandler(object instance)
        {
            m_InstanceSpecificHandlers.Remove(instance);
        }

        /// <summary>
        ///     Sets the handler to be called when no instance specific handler is registered.
        ///     The action arguments are the instance followed by the arguments.
        ///
        ///     The return value of the handler decides if the unpatched methods should be called (true)
        ///     or not (false). Note that in the case of multiple patches for one method, the call order
        ///     is inverse to the patching order (last patch gets called first). If the first patch returns
        ///     true, the next patch will be called and so on.
        /// </summary>
        /// <param name="handler"></param>
        public void SetGlobalHandler(Func<object, object, bool> handler)
        {
            if (GlobalHandler != null)
            {
                throw new ArgumentException($"Cannot have multiple global handlers for {this}.");
            }

            GlobalHandler = handler;
        }

        public void RemoveGlobalHandler()
        {
            GlobalHandler = null;
        }
    }
}
