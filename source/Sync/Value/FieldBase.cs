using System;
using JetBrains.Annotations;

namespace Sync.Value
{
    /// <summary>
    ///     Type-erased interface for setting and getting a value from an instance.
    /// </summary>
    public abstract class FieldBase
    {
        [NotNull] public readonly Type DeclaringType;
        public readonly FieldId Id;

        public FieldBase(Type declaringType)
        {
            Id = Registry.Register(this);
            DeclaringType = declaringType;
        }

        /// <summary>
        ///     Returns the current value of an instance of this <see cref="FieldBase" />.
        /// </summary>
        /// <param name="target">Instance to get the value from.</param>
        /// <exception cref="ArgumentException">If the target or value are not of the expected type.</exception>
        /// <returns>Value</returns>
        public abstract object Get(object target);

        /// <summary>
        ///     Sets the current value of an instance of this <see cref="FieldBase" />.
        /// </summary>
        /// <param name="target">Instance to set the value on.</param>
        /// <param name="value">Value to set.</param>
        /// <exception cref="ArgumentException">If the target or value are not of the expected type.</exception>
        public abstract void Set(object target, object value);

        public abstract override string ToString();
    }
}