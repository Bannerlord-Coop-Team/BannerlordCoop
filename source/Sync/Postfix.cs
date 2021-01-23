using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Behaviour;

namespace Sync
{
    public class Postfix
    {
        public delegate void InstanceHandlerDelegate(ETriggerOrigin eOrigin, object[] args);
        public delegate void GlobalHandlerDelegate(ETriggerOrigin eOrigin, object instance, object[] args);
        
        
        private readonly Dictionary<object, InstanceHandlerDelegate> m_InstanceSpecificHandlers =
            new Dictionary<object, InstanceHandlerDelegate>();

        public GlobalHandlerDelegate GlobalHandler { get; private set; }

        public IReadOnlyDictionary<object, InstanceHandlerDelegate> InstanceSpecificHandlers =>
            m_InstanceSpecificHandlers;

        /// <summary>
        ///     Sets the handler to be called when a specific instance of the <see cref="Postfix" />
        ///     was called. Multiple instance specific handlers are not supported.
        /// 
        ///     The argument passed to the action are the arguments, not the instance!
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="handler"></param>
        public void SetHandler([NotNull] object instance, [NotNull] InstanceHandlerDelegate handler)
        {
            if (m_InstanceSpecificHandlers.ContainsKey(instance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_InstanceSpecificHandlers.Add(instance, handler);
        }

        /// <summary>
        ///     Gets the handler to be called when the given instance was changed.
        /// </summary>
        /// <param name="instance"></param>
        public InstanceHandlerDelegate GetHandler(object instance)
        {
            bool bHasGlobalHandler = GlobalHandler != null;
            if (instance != null &&
                m_InstanceSpecificHandlers.TryGetValue(
                    instance,
                    out InstanceHandlerDelegate instanceSpecificHandler))
            {
                if (bHasGlobalHandler)
                {
                    return (eOrigin, args) =>
                    {
                        GlobalHandler(eOrigin, instance, args);
                        instanceSpecificHandler(eOrigin, args);
                    };
                }

                return instanceSpecificHandler;
            }

            if (GlobalHandler != null)
            {
                return (eOrigin, args) => 
                    GlobalHandler(eOrigin, instance, args);
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
        /// <param name="handler"></param>
        public void SetGlobalHandler(GlobalHandlerDelegate handler)
        {
            if (GlobalHandler != null)
            {
                throw new ArgumentException($"Cannot have multiple global postfix handlers for {this}.");
            }

            GlobalHandler = handler;
        }

        public void RemoveGlobalHandler()
        {
            GlobalHandler = null;
        }
    }
}