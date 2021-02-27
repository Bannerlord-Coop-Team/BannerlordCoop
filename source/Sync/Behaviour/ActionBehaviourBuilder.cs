using System;
using System.Collections.Generic;
using System.Linq;
using Sync.Call;
using Sync.Value;

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

        public CallBehaviourBuilder Calls(params PatchedInvokable[] methods)
        {
            if (methods.Length == 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            var behaviour = new CallBehaviourBuilder(methods.Select(m => m.Id), m_Condition);
            foreach (var method in methods) Register(method.Id, behaviour);
            return behaviour;
        }

        public FieldActionBehaviourBuilder Changes(params FieldAccess[] fields)
        {
            if (fields.Length == 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            var fieldChangeAction = new FieldActionBehaviourBuilder(fields.Select(f => f.Id), m_Condition);
            foreach (var field in fields) Register(field.Id, fieldChangeAction);

            return fieldChangeAction;
        }

        public FieldActionBehaviourBuilder Changes(FieldAccessGroup accessGroup)
        {
            var fieldChangeAction = new FieldActionBehaviourBuilder(new List<FieldId> {accessGroup.Id}, m_Condition);
            Register(accessGroup.Id, fieldChangeAction);
            return fieldChangeAction;
        }

        #region Getters

        public Dictionary<InvokableId, CallBehaviourBuilder> CallBehaviours { get; } =
            new Dictionary<InvokableId, CallBehaviourBuilder>();

        public Dictionary<FieldId, FieldActionBehaviourBuilder> FieldChangeAction { get; } =
            new Dictionary<FieldId, FieldActionBehaviourBuilder>();

        #endregion


        #region Private

        private void Register(InvokableId key, CallBehaviourBuilder behaviourBuilder)
        {
            if (CallBehaviours.ContainsKey(key))
                throw new Exception($"There's already a behaviour registered for the method '{key}'.");

            CallBehaviours[key] = behaviourBuilder;
        }

        private void Register(FieldId key, FieldActionBehaviourBuilder change)
        {
            if (FieldChangeAction.ContainsKey(key))
                throw new Exception($"There's already a behaviour registered for the field '{key}'.");

            FieldChangeAction[key] = change;
        }

        private readonly Condition m_Condition;

        #endregion
    }
}