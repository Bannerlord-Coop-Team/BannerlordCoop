using System;

namespace Sync
{
    /// <summary>
    ///     Type-erased interface for setting and getting a value from any source.
    /// </summary>
    public abstract class ValueAccess : Watchable
    {
        /// <summary>
        ///     Returns the current value of an instance of this <see cref="ValueAccess" />.
        /// </summary>
        /// <param name="target">Instance to get the value from.</param>
        /// <exception cref="ArgumentException">If the target or value are not of the expected type.</exception>
        /// <returns>Value</returns>
        public abstract object Get(object target);

        /// <summary>
        ///     Sets the current value of an instance of this <see cref="ValueAccess" />.
        /// </summary>
        /// <param name="target">Instance to set the value on.</param>
        /// <param name="value">Value to set.</param>
        /// <exception cref="ArgumentException">If the target or value are not of the expected type.</exception>
        public abstract void Set(object target, object value);
    }
}
