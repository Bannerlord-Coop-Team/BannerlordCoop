using System;

namespace Sync.Behaviour
{
    public class ActionBehaviour
    {
        public ActionBehaviour Execute()
        {
            ExecuteMethod = true;
            return this;
        }
        
        public ActionBehaviour Ignore()
        {
            ExecuteMethod = false;
            return this;
        }

        public ActionBehaviour DelegateTo(Action<object, IPendingAction> handler)
        {
            MethodCallHandler = handler;
            return this;
        }

        public bool ExecuteMethod { get; set; } = true;
        public Action<object, IPendingAction> MethodCallHandler { get; set; }
    }
}