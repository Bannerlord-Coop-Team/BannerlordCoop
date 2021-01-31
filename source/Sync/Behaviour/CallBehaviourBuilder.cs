using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Builder class to define the behaviour of a patched method or property getter / setter.
    /// </summary>
    public class CallBehaviourBuilder
    {
        public CallBehaviourBuilder()
        {
            
        }
        public CallBehaviourBuilder(IEnumerable<MethodId> ids)
        {
            m_MethodIds = ids;
        }
        /// <summary>
        ///     Propagate the call to the function to the original or the next patch (if one exists).
        /// </summary>
        public void Execute()
        {
            CallPropagationBehaviour = ECallPropagation.CallOriginal;
        }
        
        /// <summary>
        ///     Suppress the original call. No further patch nor the original will be called.
        /// </summary>
        public void Suppress()
        {
            CallPropagationBehaviour = ECallPropagation.Suppress;
        }
        
        /// <summary>
        ///     Delegate the call to a static handler. The handler can control the behaviour at runtime using the
        ///     provided <see cref="IPendingMethodCall"/> argument.
        ///
        ///     1st argument:   The method call that is being processed.
        /// </summary>
        /// <param name="handler"></param>
        public void DelegateTo(Func<IPendingMethodCall, ECallPropagation> handler)
        {
            MethodCallHandler = handler;
        }
        
        /// <summary>
        ///     Delegate the call to a static handler. The handler can control the behaviour at runtime using the
        ///     provided <see cref="IPendingMethodCall"/> argument.
        ///
        ///     1st argument:   The instance of the <see cref="CoopManaged"/> class that manages the object the method
        ///                     is being called on, i.e. `this`. null for static calls.
        ///     2nd argument:   The method call that is being processed.
        ///
        /// </summary>
        /// <param name="handler"></param>
        public void DelegateTo(Func<object, IPendingMethodCall, ECallPropagation> handler)
        {
            MethodCallHandlerInstance = handler;
        }
        
        /// <summary>
        ///     The local call will be broadcast to all clients as an authoritative call. All clients will receive the
        ///     call on the same campaign tick. The originator of the call will receive the authoritative call as well.
        /// </summary>
        public CallBehaviourBuilder Broadcast(IActionValidator validator = null)
        {
            DoBroadcast = true;
            if (validator != null)
            {
                foreach (MethodId id in m_MethodIds)
                {
                    ActionValidatorRegistry.Register(id, validator);
                }
            }
            return this;
        }
        public ECallPropagation CallPropagationBehaviour { get; private set; } = ECallPropagation.CallOriginal;
        public bool DoBroadcast { get; private set; } = false;
        [CanBeNull] public Func<IPendingMethodCall, ECallPropagation> MethodCallHandler { get; private set; }
        [CanBeNull] public Func<object, IPendingMethodCall, ECallPropagation> MethodCallHandlerInstance { get; private set; }
        
        #region Private

        private readonly IEnumerable<MethodId> m_MethodIds;

        #endregion
    }
}