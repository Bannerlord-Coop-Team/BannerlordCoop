namespace Sync.Behaviour
{
    public static class ActionValidator
    {
        public static bool IsAllowed(ValueId value)
        {
            if(ActionValidatorRegistry.TryGet(value, out IActionValidator validator))
            {
                return validator.IsAllowed();
            }

            return true;
        }
        
        public static bool IsAllowed(MethodId method)
        {
            if(ActionValidatorRegistry.TryGet(method, out IActionValidator validator))
            {
                return validator.IsAllowed();
            }

            return true;
        }
    }
}