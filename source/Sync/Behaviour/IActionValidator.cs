namespace Sync.Behaviour
{
    public enum EValidationResult
    {
        Valid,      // The action is valid, that is it may be executed.
        Invalid     // The action should not be executed.
    }
    
    /// <summary>
    ///     Validator that checks if an action, i.e. a method call or field change, is valid or not.
    /// </summary>
    public interface IActionValidator
    {
        EValidationResult Validate();
    }
}