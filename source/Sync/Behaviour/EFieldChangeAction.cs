namespace Sync.Behaviour
{
    /// <summary>
    ///     Defines the action that should be taken when a monitored field was changed by a known accessor to the
    ///     field.
    /// </summary>
    public enum EFieldChangeAction
    {
        /// <summary>
        ///     The change to the field is kept.
        /// </summary>
        Keep,

        /// <summary>
        ///     The change to the field is reverted.
        /// </summary>
        Revert
    }
}