using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sync.Behaviour;

namespace Sync
{
    /// <summary>
    ///     Base class for class wrappers that notify when specific instances of the wrapped class
    ///     change internal state.
    /// </summary>
    public class Prefix
    {
        public delegate ECallPropagation InstanceHandlerDelegate(object[] args);
        public delegate ECallPropagation GlobalHandlerDelegate(object instance, object[] args);
        public delegate ECallPropagation InstanceHandlerCallerIdDelegate(ETriggerOrigin eOrigin, object[] args);
        public delegate ECallPropagation GlobalHandlerCallerIdDelegate(ETriggerOrigin eOrigin, object instance, object[] args);
        
        
        private readonly Dictionary<WeakReference<object>, InstanceHandlerCallerIdDelegate> m_InstanceSpecificHandlers =
            new Dictionary<WeakReference<object>, InstanceHandlerCallerIdDelegate>();

        public GlobalHandlerCallerIdDelegate GlobalPrefixHandler { get; private set; }

        public IReadOnlyDictionary<WeakReference<object>, InstanceHandlerCallerIdDelegate> InstanceSpecificHandlers =>
            m_InstanceSpecificHandlers;

        /// <summary>
        ///     Sets the handler to be called when a specific instance of the <see cref="Prefix" />
        ///     requested a change. Multiple instance specific handlers are not supported.
        ///     The argument passed to the action are the arguments, not the instance!
        ///
        ///     The return value of the handler decides if the unpatched method should be called (true)
        ///     or not (false). Note that in the case of multiple patches for one method, the call order
        ///     is inverse to the patching order (last patch gets called first). If the first patch returns
        ///     true, the next patch will be called and so on.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="handler"></param>
        public void SetHandler([NotNull] object instance, [NotNull] InstanceHandlerDelegate handler)
        {
            SetHandler(instance, (eOrigin, args) =>
            {
                if (eOrigin == ETriggerOrigin.Authoritative) return ECallPropagation.CallOriginal;
                return handler.Invoke(args);
            });
        }
        
        /// <summary>
        ///     Sets the handler to be called when a specific instance of the <see cref="Prefix" />
        ///     requested a change. Multiple instance specific handlers are not supported.
        ///     The argument passed to the action are the arguments, not the instance!
        ///
        ///     The return value of the handler decides if the unpatched method should be called (true)
        ///     or not (false). Note that in the case of multiple patches for one method, the call order
        ///     is inverse to the patching order (last patch gets called first). If the first patch returns
        ///     true, the next patch will be called and so on.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="handler"></param>
        public void SetHandler([NotNull] object instance, [NotNull] InstanceHandlerCallerIdDelegate handler)
        {
            if (m_InstanceSpecificHandlers.Any(pair => pair.Key.TryGetTarget(out object o) && o == instance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_InstanceSpecificHandlers.Add(new WeakReference<object>(instance, true), handler);
        }

        /// <summary>
        ///     Gets the handler to be called when the given instance changes.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns>Handler or null</returns>
        public InstanceHandlerCallerIdDelegate GetHandler(object instance)
        {
            bool bHasGlobalHandler = GlobalPrefixHandler != null;
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
                            GlobalPrefixHandler(eOrigin, instance, args);
                            return instanceSpecificHandler(eOrigin, args);
                        };
                    }
                    return instanceSpecificHandler;
                }
            }

            if (GlobalPrefixHandler != null)
            {
                return (eOrigin, args) => 
                    GlobalPrefixHandler(eOrigin, instance, args);
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
        ///
        ///     The return value of the handler decides if the unpatched methods should be called (true)
        ///     or not (false). Note that in the case of multiple patches for one method, the call order
        ///     is inverse to the patching order (last patch gets called first). If the first patch returns
        ///     true, the next patch will be called and so on.
        /// </summary>
        /// <param name="handler"></param>
        public void SetGlobalHandler(GlobalHandlerDelegate handler)
        {
            SetGlobalHandler((eOrigin, instance, args) =>
            {
                if (eOrigin == ETriggerOrigin.Authoritative)
                {
                    // Default behaviour: Authority is always applied
                    return ECallPropagation.CallOriginal;
                }
                return handler.Invoke(instance, args);
            });
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
        public void SetGlobalHandler(GlobalHandlerCallerIdDelegate handler)
        {
            if (GlobalPrefixHandler != null)
            {
                throw new ArgumentException($"Cannot have multiple global prefix handlers for {this}.");
            }

            GlobalPrefixHandler = handler;
        }

        public void RemoveGlobalHandler()
        {
            GlobalPrefixHandler = null;
        }
    }
}
