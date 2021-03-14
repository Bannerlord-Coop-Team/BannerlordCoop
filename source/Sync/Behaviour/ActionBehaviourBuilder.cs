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
    public class ActionBehaviourBuilder : ConditionalBehaviour
    {
        public ActionBehaviourBuilder([CamBeNull] Condition condition) : base(condition)
        {
        }
        /// <summary>
        ///     Constructs a new behaviour for method calls.
        /// </summary>
        /// <param name="methods">Methods that the behaviour applies to.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public CallBehaviourBuilder Calls(params PatchedInvokable[] methods)
        {
            if (methods.Length == 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            var behaviour = new CallBehaviourBuilder(methods.Select(m => m.Id), Condition);
            foreach (var method in methods) Register(method.Id, behaviour);
            return behaviour;
        }
        /// <summary>
        ///     Constructs a new behaviour for monitored field changes.
        /// </summary>
        /// <param name="fields">Fields that the behaviour applies to.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public FieldAccessBehaviourBuilder Changes(params FieldAccess[] fields)
        {
            if (fields.Length == 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            var fieldChangeAction = new FieldAccessBehaviourBuilder(fields.Select(f => f.Id), Condition);
            foreach (var field in fields) Register(field.Id, fieldChangeAction);

            return fieldChangeAction;
        }
        /// <summary>
        ///     Constructs a new behaviour for a monitored field group.
        /// </summary>
        /// <param name="accessGroup">Field group that the behaviour applies to.</param>
        /// <returns></returns>
        public FieldAccessBehaviourBuilder Changes(FieldAccessGroup accessGroup)
        {
            var fieldChangeAction = new FieldAccessBehaviourBuilder(new List<FieldId> {accessGroup.Id}, Condition);
            Register(accessGroup.Id, fieldChangeAction);
            return fieldChangeAction;
        }

        #region Getters
        /// <summary>
        ///     Returns all defined call behaviours.
        /// </summary>
        public Dictionary<InvokableId, CallBehaviourBuilder> CallBehaviours { get; } =
            new Dictionary<InvokableId, CallBehaviourBuilder>();

        /// <summary>
        ///     Returns all defined monitored field change behaviours.
        /// </summary>
        public Dictionary<FieldId, FieldAccessBehaviourBuilder> FieldChangeAction { get; } =
            new Dictionary<FieldId, FieldAccessBehaviourBuilder>();

        #endregion


        #region Private
        private void Register(InvokableId key, CallBehaviourBuilder behaviourBuilder)
        {
            if (CallBehaviours.ContainsKey(key))
                throw new Exception($"There's already a behaviour registered for the method '{key}'.");

            CallBehaviours[key] = behaviourBuilder;
        }

        private void Register(FieldId key, FieldAccessBehaviourBuilder change)
        {
            if (FieldChangeAction.ContainsKey(key))
                throw new Exception($"There's already a behaviour registered for the field '{key}'.");

            FieldChangeAction[key] = change;
        }

        #endregion
    }

    public class CamBeNullAttribute : Attribute
    {
    }
}