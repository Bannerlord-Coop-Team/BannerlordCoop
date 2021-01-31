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
        public ActionBehaviourBuilder(ActionBehaviour.IsApplicableDelegate decider)
        {
            m_Decider = decider;
        }
        public CallBehaviourBuilder Calls(params MethodAccess[] methods)
        {
            var behaviour = new CallBehaviourBuilder(methods.Select(m => m.Id), m_Decider);
            foreach (var method in methods)
            {
                Register(method.Id, behaviour);
            }
            return behaviour;
        }
        public FieldActionBehaviourBuilder Changes(params FieldAccess[] fields)
        {
            var fieldChangeAction = new FieldActionBehaviourBuilder(fields.Select(f => f.Id), m_Decider);
            foreach (FieldAccess field in fields)
            {
                Register(field.Id, fieldChangeAction);
            }

            return fieldChangeAction;
        }

        #region Getters
        public Dictionary<MethodId, CallBehaviourBuilder> CallBehaviours { get; } = new Dictionary<MethodId, CallBehaviourBuilder>();
        public Dictionary<FieldId, FieldActionBehaviourBuilder> FieldChangeAction { get; } = new Dictionary<FieldId, FieldActionBehaviourBuilder>();
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

        private void Register(FieldId key, FieldActionBehaviourBuilder change)
        {
            if (FieldChangeAction.ContainsKey(key))
            {
                throw new Exception($"There's already a behaviour registered for the field '{key}'.");
            }

            FieldChangeAction[key] = change;
        }
        
        private ActionBehaviour.IsApplicableDelegate m_Decider;

        #endregion
    }
}