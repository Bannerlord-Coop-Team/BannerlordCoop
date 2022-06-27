using System.Collections.Generic;
using System.Linq;
using Sync.Call;
using Sync.Value;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Builder class to define the accessors to a field in order to define it's behaviour.
    ///     Note that field changes cannot be intercepted (patched) directly, instead methods and property setters
    ///     that access the field are patched - these are referred to as 'accessors' to the field.
    /// </summary>
    public class FieldAccessBehaviourBuilder
    {
        /// <summary>
        ///     The method associations to all fields that need to be intercepted.
        /// </summary>
        public readonly static Dictionary<PatchedInvokable, List<FieldId>> MethodsWithPatchedFields =
            new Dictionary<PatchedInvokable, List<FieldId>>();
        public FieldAccessBehaviourBuilder(IEnumerable<FieldId> fieldIds, Condition condition)
        {
            m_FieldIds = fieldIds;
            m_Condition = condition;
        }
        /// <summary>
        ///     The patched invokable used to access the fields.
        /// </summary>
        public List<PatchedInvokable> Accessors { get; } = new List<PatchedInvokable>();
        /// <summary>
        ///     The behaviour builder for the managed field itself.
        /// </summary>
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

            foreach(var accessor in Accessors)
            {
                if (MethodsWithPatchedFields.ContainsKey(accessor))
                {
                    MethodsWithPatchedFields[accessor].AddRange(m_FieldIds);
                }
                else
                {
                    MethodsWithPatchedFields.Add(accessor, new List<FieldId>(m_FieldIds));
                }
            }

            return Behaviour;
        }

        #region Private

        private readonly IEnumerable<FieldId> m_FieldIds;
        private readonly Condition m_Condition;

        #endregion
    }
}