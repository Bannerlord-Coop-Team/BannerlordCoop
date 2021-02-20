using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Invokable;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Builder class to define the behaviour of a patched method or property getter / setter.
    /// </summary>
    public class CallBehaviourBuilder : ActionBehaviour
    {
        #region Private

        private readonly IEnumerable<InvokableId> m_MethodIds;

        #endregion

        public CallBehaviourBuilder(IEnumerable<InvokableId> ids, Condition condition) : base(condition)
        {
            m_MethodIds = ids;
        }

        public ECallPropagation CallPropagationBehaviour { get; private set; } = ECallPropagation.CallOriginal;
        public bool DoBroadcast { get; private set; }
        public Func<ISynchronization> SynchronizationFactory { get; private set; }
        [CanBeNull] public Func<IPendingMethodCall, ECallPropagation> MethodCallHandler { get; private set; }

        [CanBeNull]
        public Func<object, IPendingMethodCall, ECallPropagation> MethodCallHandlerInstance { get; private set; }

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
        public void Skip()
        {
            CallPropagationBehaviour = ECallPropagation.Skip;
        }

        /// <summary>
        ///     Delegate the call to a static handler. The handler can control the behaviour at runtime using the
        ///     provided <see cref="IPendingMethodCall" /> argument.
        ///     1st argument:   The method call that is being processed.
        /// </summary>
        /// <param name="handler"></param>
        public void DelegateTo(Func<IPendingMethodCall, ECallPropagation> handler)
        {
            MethodCallHandler = handler;
        }

        /// <summary>
        ///     Delegate the call to a static handler. The handler can control the behaviour at runtime using the
        ///     provided <see cref="IPendingMethodCall" /> argument.
        ///     1st argument:   The instance of the <see cref="CoopManaged" /> class that manages the object the method
        ///     is being called on, i.e. `this`. null for static calls.
        ///     2nd argument:   The method call that is being processed.
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
        public CallBehaviourBuilder Broadcast([NotNull] Func<ISynchronization> syncFactory,
            IActionValidator validator = null)
        {
            SynchronizationFactory = syncFactory;
            DoBroadcast = true;
            if (validator != null)
                foreach (var id in m_MethodIds)
                    ActionValidatorRegistry.Register(id, validator);
            return this;
        }
    }
}