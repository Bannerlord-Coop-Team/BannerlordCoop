using Sync.Invokable;
using Sync.Value;

namespace Sync.Behaviour
{
    public static class ActionValidator
    {
        public static bool IsAllowed(FieldId field)
        {
            if (ActionValidatorRegistry.TryGet(field, out var validator)) return validator.IsAllowed();

            return true;
        }

        public static bool IsAllowed(InvokableId invokable)
        {
            if (ActionValidatorRegistry.TryGet(invokable, out var validator)) return validator.IsAllowed();

            return true;
        }
    }
}