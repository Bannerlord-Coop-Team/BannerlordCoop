using System;
using JetBrains.Annotations;

namespace Sync.Behaviour
{
    public class CallBehaviour
    {
        public CallBehaviour(bool isBroadcastAllowed)
        {
            m_IsBroadcastAllowed = isBroadcastAllowed;
        }
        public void Execute()
        {
            CallPropagationBehaviour = ECallPropagation.CallOriginal;
        }
        public void Suppress()
        {
            CallPropagationBehaviour = ECallPropagation.Suppress;
        }
        public void DelegateTo(Func<IPendingMethodCall, ECallPropagation> handler)
        {
            MethodCallHandler = handler;
        }
        /// <summary>
        ///     The local call will be broadcast to all clients as an authoritative call. All clients will receive the
        ///     call on the same campaign tick. The originator of the call will receive the authoritative call as well.
        /// </summary>
        public CallBehaviour Broadcast()
        {
            if (!m_IsBroadcastAllowed)
            {
                throw new Exception("Invalid call behaviour: Not allowed to broadcast an authoritative call.");
            }
            DoBroadcast = true;
            return this;
        }
        public ECallPropagation CallPropagationBehaviour { get; private set; } = ECallPropagation.CallOriginal;
        public bool DoBroadcast { get; private set; } = false;
        [CanBeNull] public Func<IPendingMethodCall, ECallPropagation> MethodCallHandler { get; private set; }

        private readonly bool m_IsBroadcastAllowed;
    }
}