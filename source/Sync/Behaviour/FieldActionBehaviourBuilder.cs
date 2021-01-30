using System.Collections.Generic;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Builder class to define the accessors to a field in order to define it's behaviour.
    ///
    ///     Note that field changes cannot be intercepted (patched) directly, instead methods and property setters
    ///     that access the field are patched - these are referred to as 'accessors' to the field.
    /// </summary>
    public class FieldActionBehaviourBuilder
    {
        /// <summary>
        ///     Defines the accessors through which the field is changed. i.e. the methods and property setters
        ///     that change the field. These will be patched in order to monitor the field for changes.
        /// </summary>
        /// <param name="accessors">List of all accessors.</param>
        /// <returns>Behaviour builder for the field.</returns>
        public FieldBehaviourBuilder Through(params MethodAccess[] accessors)
        {
            Accessors.AddRange(accessors);
            return new FieldBehaviourBuilder();
        }

        public List<MethodAccess> Accessors { get; } = new List<MethodAccess>();
    }
}