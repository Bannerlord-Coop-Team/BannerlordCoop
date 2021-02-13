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
        public ActionBehaviourBuilder(Condition condition)
        {
            m_Condition = condition;
        }
        public CallBehaviourBuilder Calls(params MethodAccess[] methods)
        {
            var behaviour = new CallBehaviourBuilder(methods.Select(m => m.Id), m_Condition);
            foreach (var method in methods)
            {
                Register(method.Id, behaviour);
            }
            return behaviour;
        }
        public FieldActionBehaviourBuilder Changes(params FieldAccess[] fields)
        {
            var fieldChangeAction = new FieldActionBehaviourBuilder(fields.Select(f => f.Id), m_Condition);
            foreach (FieldAccess field in fields)
            {
                Register(field.Id, fieldChangeAction);
            }

            return fieldChangeAction;
        }
        public FieldActionBehaviourBuilder Changes(FieldAccessGroup group)
        {
            var fieldChangeAction = new FieldActionBehaviourBuilder(new List<ValueId>(){group.Id}, m_Condition);
            Register(group.Id, fieldChangeAction);
            return fieldChangeAction;
        }

        #region Getters
        public Dictionary<MethodId, CallBehaviourBuilder> CallBehaviours { get; } = new Dictionary<MethodId, CallBehaviourBuilder>();
        public Dictionary<ValueId, FieldActionBehaviourBuilder> FieldChangeAction { get; } = new Dictionary<ValueId, FieldActionBehaviourBuilder>();
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

        private void Register(ValueId key, FieldActionBehaviourBuilder change)
        {
            if (FieldChangeAction.ContainsKey(key))
            {
                throw new Exception($"There's already a behaviour registered for the field '{key}'.");
            }

            FieldChangeAction[key] = change;
        }
        
        private Condition m_Condition;

        #endregion
    }
}