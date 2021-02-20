using System.Collections.Generic;
using Sync.Invokable;
using Sync.Value;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Builder class to define the accessors to a field in order to define it's behaviour.
    ///     Note that field changes cannot be intercepted (patched) directly, instead methods and property setters
    ///     that access the field are patched - these are referred to as 'accessors' to the field.
    /// </summary>
    public class FieldActionBehaviourBuilder
    {
        public FieldActionBehaviourBuilder(IEnumerable<FieldId> fieldIds, Condition condition)
        {
            m_FieldIds = fieldIds;
            m_Condition = condition;
        }

        public List<PatchedInvokable> Accessors { get; } = new List<PatchedInvokable>();
        public FieldBehaviourBuilder Behaviour { get; private set; }

        /// <summary>
        ///     Defines the accessors through which the field is changed. i.e. the methods and property setters
        ///     that change the field. These will be patched in order to monitor the field for changes.
        /// </summary>
        /// <param name="accessors">List of all accessors.</param>
        /// <returns>Behaviour builder for the field.</returns>
        public FieldBehaviourBuilder Through(params PatchedInvokable[] accessors)
        {
            Accessors.AddRange(accessors);
            Behaviour = new FieldBehaviourBuilder(m_FieldIds, m_Condition);
            return Behaviour;
        }

        #region Private

        private readonly IEnumerable<FieldId> m_FieldIds;
        private readonly Condition m_Condition;

        #endregion
    }
}