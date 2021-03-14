namespace Sync.Value
{
    /// <summary>
    ///     Represents a request to change a field value.
    /// </summary>
    public class FieldChangeRequest
    {
        /// <summary>
        ///     The value set in the instance before the change request was recorded.
        /// </summary>
        public object OriginalValue { get; set; }

        /// <summary>
        ///     The value that was buffered.
        /// </summary>
        public object RequestedValue { get; set; }
    }
}