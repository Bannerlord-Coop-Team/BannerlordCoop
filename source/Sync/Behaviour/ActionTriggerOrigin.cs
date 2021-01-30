using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Sync.Behaviour
{
    public class ActionTriggerOrigin
    {
        public ActionTriggerOrigin(bool isBroadcastAllowed)
        {
            m_IsBroadcastAllowed = isBroadcastAllowed;
        }
        public CallBehaviour Calls(params MethodAccess[] methods)
        {
            var behaviour = new CallBehaviour(m_IsBroadcastAllowed);
            foreach (var method in methods)
            {
                
                Register(method.Id, behaviour);
            }
            return behaviour;
        }
        private void Register(MethodId key, CallBehaviour behaviour)
        {
            if (Behaviours.ContainsKey(key))
            {
                throw new Exception("There's already a behaviour registered for the method.");
            }

            Behaviours[key] = behaviour;
        }
        
        public Dictionary<MethodId, CallBehaviour> Behaviours { get; } = new Dictionary<MethodId, CallBehaviour>();

        [NotNull]
        public CallBehaviour GetBehaviour(MethodId methodId)
        {
            return Behaviours.TryGetValue(methodId, out CallBehaviour behaviours) ? behaviours : new CallBehaviour(m_IsBroadcastAllowed);
        }
        
        private readonly bool m_IsBroadcastAllowed;
    }
}