namespace Sync.Behaviour
{
    public enum EValidationResult
    {
        Valid,
        Invalid
    }
    
    public interface IActionValidator
    {
        EValidationResult Validate();
    }
}