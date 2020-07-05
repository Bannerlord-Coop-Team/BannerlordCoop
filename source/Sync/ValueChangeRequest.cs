namespace Sync
{
    public class ValueChangeRequest
    {
        /// <summary>
        ///     The value set in the instance when it was checked the last time.
        /// </summary>
        public object LatestActualValue { get; set; }

        /// <summary>
        ///     The value that was buffered.
        /// </summary>
        public object RequestedValue { get; set; }

        /// <summary>
        ///     True when <see cref="RequestedValue" /> was acknowledged.
        /// </summary>
        public bool RequestProcessed { get; set; }
    }
}
