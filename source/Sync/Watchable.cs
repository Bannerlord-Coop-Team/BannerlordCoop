using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Sync
{
    public abstract class Watchable
    {
        private readonly Dictionary<object, Action<object>> m_InstanceSpecificHandlers =
            new Dictionary<object, Action<object>>();

        private Action<object, object> m_GlobalHandler;

        /// <summary>
        ///     Sets the handler to be called when a specific instance of the <see cref="Watchable" />
        ///     requested a change. Multiple instance specific handlers are not supported.
        ///     The argument passed to the action are the arguments, not the instance!
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="action"></param>
        public void SetInstanceHandler([NotNull] object instance, Action<object> action)
        {
            if (m_InstanceSpecificHandlers.ContainsKey(instance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_InstanceSpecificHandlers.Add(instance, action);
        }

        public Action<object> GetHandler(object syncableInstance)
        {
            bool bHasGlobalHandler = m_GlobalHandler != null;
            if (syncableInstance != null &&
                m_InstanceSpecificHandlers.TryGetValue(
                    syncableInstance,
                    out Action<object> instanceSpecificHandler))
            {
                if (bHasGlobalHandler)
                {
                    return args =>
                    {
                        m_GlobalHandler(syncableInstance, args);
                        instanceSpecificHandler(args);
                    };
                }

                return instanceSpecificHandler;
            }

            if (m_GlobalHandler != null)
            {
                return args => m_GlobalHandler(syncableInstance, args);
            }

            return null;
        }

        public void RemoveInstanceHandler(object syncableInstance)
        {
            m_InstanceSpecificHandlers.Remove(syncableInstance);
        }

        /// <summary>
        ///     Sets the handler to be called when no instance specific handler is registred.
        ///     The action arguments are the instance followed by the arguments.
        /// </summary>
        /// <param name="syncableInstance"></param>
        /// <param name="action"></param>
        public void SetGlobalHandler(Action<object, object> action)
        {
            if (m_GlobalHandler != null)
            {
                throw new ArgumentException("Cannot have multiple global sync handlers.");
            }

            m_GlobalHandler = action;
        }

        public void RemoveGlobalHandler()
        {
            m_GlobalHandler = null;
        }
    }
}
