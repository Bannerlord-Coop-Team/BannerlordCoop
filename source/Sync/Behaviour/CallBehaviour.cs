using System;
using JetBrains.Annotations;

namespace Sync.Behaviour
{
    public class CallBehaviour
    {
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

        public ECallPropagation CallPropagationBehaviour { get; private set; } = ECallPropagation.CallOriginal;
        [CanBeNull] public Func<IPendingMethodCall, ECallPropagation> MethodCallHandler { get; private set; }
    }
}