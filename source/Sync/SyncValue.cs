namespace Sync
{
    public abstract class SyncValue : Watchable
    {
        /// <summary>
        ///     Returns the current value of an instance of this syncable.
        /// </summary>
        /// <param name="target">Instance.</param>
        /// <returns></returns>
        public abstract object Get(object target);

        /// <summary>
        ///     Sets the current value of an instance of this syncable.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="value"></param>
        public abstract void Set(object target, object value);
    }
}
