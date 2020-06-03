using System;

namespace Sync
{
    /// <summary>
    ///     Type-erased interface for setting and getting a value from an instance.
    /// </summary>
    public abstract class ValueAccess : Watchable
    {
        /// <summary>
        ///     Returns the type of an instance the value is declared in.
        /// </summary>
        /// <returns>Declaring type</returns>
        public abstract Type GetDeclaringType();

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
