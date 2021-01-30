using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sync.Behaviour;

namespace Sync
{
    public class Postfix
    {
        public delegate void InstanceHandlerDelegate(EActionOrigin eOrigin, object[] args);
        public delegate void GlobalHandlerDelegate(EActionOrigin eOrigin, object instance, object[] args);
        
        
        private readonly Dictionary<WeakReference<object>, InstanceHandlerDelegate> m_InstanceSpecificHandlers =
            new Dictionary<WeakReference<object>, InstanceHandlerDelegate>();

        public GlobalHandlerDelegate GlobalHandler { get; private set; }

        public IReadOnlyDictionary<WeakReference<object>, InstanceHandlerDelegate> InstanceSpecificHandlers =>
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
            if (m_InstanceSpecificHandlers.Any(pair => pair.Key.TryGetTarget(out object o) && o == instance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_InstanceSpecificHandlers.Add(new WeakReference<object>(instance, true), handler);
        }

        /// <summary>
        ///     Gets the handler to be called when the given instance was changed.
        /// </summary>
        /// <param name="instance"></param>
        public InstanceHandlerDelegate GetHandler(object instance)
        {
            bool bHasGlobalHandler = GlobalHandler != null;
            if (instance != null)
            {
                var instanceHandlers = m_InstanceSpecificHandlers
                    .Where(pair => pair.Key.TryGetTarget(out object o) && o == instance)
                    .Select(pair => pair.Value)
                    .ToList();
                if (instanceHandlers.Count > 0)
                {
                    var instanceSpecificHandler = instanceHandlers[0];
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
            var key = m_InstanceSpecificHandlers
                .Where(pair => pair.Key.TryGetTarget(out object o) && o == instance)
                .Select(pair => pair.Key)
                .FirstOrDefault();
            if (key != null)
            {
                m_InstanceSpecificHandlers.Remove(key);
            }
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