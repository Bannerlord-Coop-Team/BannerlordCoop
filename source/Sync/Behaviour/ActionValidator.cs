namespace Sync.Behaviour
{
    public static class ActionValidator
    {
        public static bool IsValid(FieldId field)
        {
            if(ActionValidatorRegistry.TryGet(field, out IActionValidator validator))
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