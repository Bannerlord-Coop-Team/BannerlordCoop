namespace Sync.Behaviour
{
    public static class ActionValidator
    {
        public static bool IsValid(ValueId value)
        {
            if(ActionValidatorRegistry.TryGet(value, out IActionValidator validator))
            {
                return validator.Validate() == EValidationResult.Valid;
            }

            return true;
        }
        
        public static bool IsValid(MethodId method)
        {
            if(ActionValidatorRegistry.TryGet(method, out IActionValidator validator))
            {
                return validator.Validate() == EValidationResult.Valid;
            }

            return true;
        }
    }
}