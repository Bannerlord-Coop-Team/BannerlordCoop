namespace Sync.Behaviour
{
    /// <summary>
    ///     Validator that checks if an action, i.e. a method call or field change, is valid or not.
    /// </summary>
    public interface IActionValidator
    {
        /// <summary>
        ///     Returns whether the action is currently allowed.
        /// </summary>
        /// <returns></returns>
        bool IsAllowed();

        /// <summary>
        ///     Returns a description of why the action is not allowed.
        /// </summary>
        /// <returns></returns>
        string GetReasonForRejection();
    }
}