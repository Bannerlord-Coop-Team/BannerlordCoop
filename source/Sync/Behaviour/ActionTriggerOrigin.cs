using System.Collections.Generic;
using JetBrains.Annotations;

namespace Sync.Behaviour
{
    public class ActionTriggerOrigin
    {
        public ActionBehaviour Calls(IEnumerable<MethodAccess> methods)
        {
            var behaviour = new ActionBehaviour();
            foreach (var method in methods)
            {
                Register(method.Id, behaviour);
            }
            return behaviour;
        }
        
        private void Register(MethodId key, ActionBehaviour behaviour)
        {
            if (!Behaviours.TryGetValue(key, out var methodBehaviours))
            {
                methodBehaviours = new List<ActionBehaviour>();
                Behaviours.Add(key, methodBehaviours);
            }
            methodBehaviours.Add(behaviour);
        }
        
        public Dictionary<MethodId, List<ActionBehaviour>> Behaviours { get; } = new Dictionary<MethodId, List<ActionBehaviour>>();

        [CanBeNull]
        public List<ActionBehaviour> GetBehaviour(MethodId methodId)
        {
            return Behaviours.TryGetValue(methodId, out List<ActionBehaviour> behaviours) ? behaviours : null;
        }
    }
}