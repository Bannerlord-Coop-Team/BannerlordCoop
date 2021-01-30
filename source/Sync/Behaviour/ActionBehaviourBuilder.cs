using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Builder class to define the behaviour of a patched method call, property getter / setter or monitored
    ///     field change.
    /// </summary>
    public class ActionBehaviourBuilder
    {
        public CallBehaviourBuilder Calls(params MethodAccess[] methods)
        {
            var behaviour = new CallBehaviourBuilder();
            foreach (var method in methods)
            {
                
                Register(method.Id, behaviour);
            }
            return behaviour;
        }
        public FieldActionBehaviourBuilder Changes(params FieldAccess[] fields)
        {
            var fieldChangeAction = new FieldActionBehaviourBuilder();
            foreach (var field in fields)
            {
                Register(field, fieldChangeAction);
            }

            return fieldChangeAction;
        }

        #region Getters
        public Dictionary<MethodId, CallBehaviourBuilder> CallBehaviours { get; } = new Dictionary<MethodId, CallBehaviourBuilder>();
        public Dictionary<FieldAccess, FieldActionBehaviourBuilder> FieldChangeAction { get; } = new Dictionary<FieldAccess, FieldActionBehaviourBuilder>();
        [NotNull]
        public CallBehaviourBuilder GetCallBehaviour(MethodId methodId)
        {
            return CallBehaviours.TryGetValue(methodId, out CallBehaviourBuilder behaviours) ? behaviours : new CallBehaviourBuilder();
        }
        [NotNull]
        public FieldActionBehaviourBuilder GetFieldBehaviour(FieldAccess fieldAccess)
        {
            return FieldChangeAction.TryGetValue(fieldAccess, out FieldActionBehaviourBuilder behaviour) ? behaviour : new FieldActionBehaviourBuilder();
        }
        #endregion
        
        
        #region Private
        private void Register(MethodId key, CallBehaviourBuilder behaviourBuilder)
        {
            if (CallBehaviours.ContainsKey(key))
            {
                throw new Exception($"There's already a behaviour registered for the method '{key}'.");
            }

            CallBehaviours[key] = behaviourBuilder;
        }

        private void Register(FieldAccess key, FieldActionBehaviourBuilder change)
        {
            if (FieldChangeAction.ContainsKey(key))
            {
                throw new Exception($"There's already a behaviour registered for the field '{key}'.");
            }

            FieldChangeAction[key] = change;
        }
        #endregion
    }
}