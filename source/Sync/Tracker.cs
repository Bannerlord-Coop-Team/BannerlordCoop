using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Behaviour;

namespace Sync
{
    /// <summary>
    ///     Base class for class wrappers that notify when specific instances of the wrapped class
    ///     change internal state.
    /// </summary>
    public abstract class Tracker
    {
        private readonly Dictionary<object, Action<object>> m_InstanceSpecificHandlers =
            new Dictionary<object, Action<object>>();

        public Action<object, object> GlobalHandler { get; private set; }

        public IReadOnlyDictionary<object, Action<object>> InstanceSpecificHandlers =>
            m_InstanceSpecificHandlers;

        /// <summary>
        ///     Sets the handler to be called when a specific instance of the <see cref="Tracker" />
        ///     requested a change. Multiple instance specific handlers are not supported.
        ///     The argument passed to the action are the arguments, not the instance!
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="action"></param>
        public void SetHandler([NotNull] object instance, [NotNull] Action<object> action)
        {
            if (m_InstanceSpecificHandlers.ContainsKey(instance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_InstanceSpecificHandlers.Add(instance, action);
        }

        /// <summary>
        ///     Gets the handler to be called when the given instance changes.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns>Handler or null</returns>
        public Action<object> GetHandler(object instance)
        {
            bool bHasGlobalHandler = GlobalHandler != null;
            if (instance != null &&
                m_InstanceSpecificHandlers.TryGetValue(
                    instance,
                    out Action<object> instanceSpecificHandler))
            {
                if (bHasGlobalHandler)
                {
                    return args =>
                    {
                        GlobalHandler(instance, args);
                        instanceSpecificHandler(args);
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
        /// </summary>
        /// <param name="action"></param>
        public void SetGlobalHandler(Action<object, object> action)
        {
            if (GlobalHandler != null)
            {
                throw new ArgumentException($"Cannot have multiple global handlers for {this}.");
            }

            GlobalHandler = action;
        }

        public void RemoveGlobalHandler()
        {
            GlobalHandler = null;
        }
    }
}
