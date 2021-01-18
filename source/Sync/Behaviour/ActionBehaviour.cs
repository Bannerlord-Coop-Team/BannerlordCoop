using System;

namespace Sync.Behaviour
{
    public class ActionBehaviour
    {
        public ActionBehaviour Execute()
        {
            CallPropagationBehaviour = ECallPropagation.CallOriginal;
            return this;
        }
        
        public ActionBehaviour Suppress()
        {
            CallPropagationBehaviour = ECallPropagation.Suppress;
            return this;
        }

        public ActionBehaviour DelegateTo(Action<object, IPendingAction> handler)
        {
            MethodCallHandler = handler;
            return this;
        }

        public ECallPropagation CallPropagationBehaviour { get; private set; } = ECallPropagation.CallOriginal;
        public Action<object, IPendingAction> MethodCallHandler { get; set; }
    }
}