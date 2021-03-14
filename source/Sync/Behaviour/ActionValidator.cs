using Sync.Call;
using Sync.Value;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Static lookup table to check if an action may be executed right now. Validators are created trough
    ///     <see cref="FieldBehaviourBuilder"/> and <see cref="CallBehaviourBuilder"/>.
    /// </summary>
    public static class ActionValidator
    {
        /// <summary>
        ///     Evaluates the validator for the given field (if one exists).
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static bool IsAllowed(FieldId field)
        {
            if (TryGet(field, out var validator)) return validator.IsAllowed();

            return true;
        }
        /// <summary>
        ///     Evaluates the validator for the given invokable (if one exists).
        /// </summary>
        /// <param name="invokable"></param>
        /// <returns></returns>
        public static bool IsAllowed(InvokableId invokable)
        {
            if (TryGet(invokable, out var validator)) return validator.IsAllowed();

            return true;
        }
        /// <summary>
        ///     Returns the validator for the given field (if one exists).
        /// </summary>
        /// <param name="id"></param>
        /// <param name="validator"></param>
        /// <returns></returns>
        public static bool TryGet(FieldId id, out IActionValidator validator)
        {
            return ActionValidatorRegistry.TryGet(id, out validator);
        }
        /// <summary>
        ///     Returns the validator for the given invokable (if one exists).
        /// </summary>
        /// <param name="id"></param>
        /// <param name="validator"></param>
        /// <returns></returns>
        public static bool TryGet(InvokableId id, out IActionValidator validator)
        {
            return ActionValidatorRegistry.TryGet(id, out validator);
        }
    }
}