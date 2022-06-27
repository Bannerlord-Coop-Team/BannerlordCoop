using JetBrains.Annotations;
using Sync.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sync.Call
{
    public class Transpiler
    {
        public delegate ECallPropagation GlobalHandlerDelegate(EOriginator eOrigin, object instance);

        public delegate ECallPropagation InstanceHandlerDelegate(object[] args);

        public delegate ECallPropagation InstanceHandlerCallerIdDelegate(EOriginator eOrigin, object[] args);

        public GlobalHandlerDelegate GlobalHandler { get; private set; }

        private readonly ConditionalWeakTable<object, InstanceHandlerCallerIdDelegate> m_InstanceSpecificHandlers =
            new ConditionalWeakTable<object, InstanceHandlerCallerIdDelegate>();

        /// <summary>
        ///     Sets the handler to be called when a specific instance of the <see cref="Transpiler" />
        ///     requested a change. Multiple instance specific handlers are not supported.
        ///     The argument passed to the action are the arguments, not the instance!
        ///     The return value of the handler decides if the unpatched field should be called (true)
        ///     or not (false). Note that in the case of multiple patches for one method, the call order
        ///     is inverse to the patching order (last patch gets called first). If the first patch returns
        ///     true, the next patch will be called and so on.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="handler"></param>
        public void SetHandler([NotNull] object instance, [NotNull] InstanceHandlerCallerIdDelegate handler)
        {
            lock (m_InstanceSpecificHandlers)
            {
                if (m_InstanceSpecificHandlers.TryGetValue(instance, out InstanceHandlerCallerIdDelegate _))
                    throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");

                m_InstanceSpecificHandlers.Add(instance, handler);
            }
        }

        /// <summary>
        ///     Gets the handler to be called when the given instance changes.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns>Handler or null</returns>
        public InstanceHandlerCallerIdDelegate GetHandler(object instance)
        {
            lock (m_InstanceSpecificHandlers)
            {
                if (m_InstanceSpecificHandlers.TryGetValue(instance, out InstanceHandlerCallerIdDelegate value))
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        ///     Removes an instance specific handler.
        /// </summary>
        /// <param name="instance"></param>
        public void RemoveHandler(object instance)
        {
            lock (m_InstanceSpecificHandlers)
            {
                m_InstanceSpecificHandlers.Remove(instance);
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
                throw new ArgumentException($"Cannot have multiple global postfix handlers for {this}.");

            GlobalHandler = handler;
        }

        public void RemoveGlobalHandler()
        {
            GlobalHandler = null;
        }
    }
}
