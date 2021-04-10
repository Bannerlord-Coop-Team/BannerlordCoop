using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Sync.Value
{
    /// <summary>
    ///     A collection of <see cref="FieldAccess" /> that are declared in the same class. All fields of
    ///     the group are manipulated as a single unit. The value of each individual field is stored in an
    ///     <typeparamref name="TValueObject" /> instance.
    /// </summary>
    /// <typeparam name="TDeclaring">Class that declares all fields contained in this group.</typeparam>
    /// <typeparam name="TValueObject">
    ///     Class that can store the values for all fields.
    /// </typeparam>
    public class FieldAccessGroup<TDeclaring, TValueObject> : FieldAccessGroup
        where TValueObject : class, IEnumerable<object>
    {
        [NotNull] private readonly FieldAccess[] m_Fields;

        public FieldAccessGroup([NotNull] IEnumerable<FieldAccess> fields) : base(typeof(TDeclaring))
        {
            m_Fields = fields.ToArray();
            VerifyConstructor();
        }

        public override IEnumerable<FieldAccess> Fields => m_Fields;

        private static void VerifyConstructor()
        {
            var constructor =
                typeof(TValueObject).GetConstructor(new[] {typeof(IEnumerable<object>[])});
            if (constructor == null)
                throw new ArgumentException($"{typeof(TValueObject)} has no matching constructor.");
        }

        /// <inheritdoc />
        public override object Get(object target)
        {
            if (target is TDeclaring castedTarget) return GetTyped(castedTarget);

            throw new ArgumentException(
                $"Invalid argument. Expected {typeof(TDeclaring)}. Got {target.GetType()}.",
                nameof(target));
        }

        /// <inheritdoc />
        public override void Set(object target, object value)
        {
            if (target is TDeclaring castedTarget && value is TValueObject castedValue)
                SetTyped(castedTarget, castedValue);
            else
                throw new ArgumentException(
                    $"Invalid arguments. Expected {typeof(TDeclaring)}, {typeof(TValueObject)}. Got {target.GetType()}, {value.GetType()}.");
        }

        /// <summary>
        ///     Get the value object of this group.
        /// </summary>
        /// <param name="target">Declaring class instance to get the field values from.</param>
        /// <returns>Value object containing the values of all child fields of this collection</returns>
        public TValueObject GetTyped(TDeclaring target)
        {
            return (TValueObject) typeof(TValueObject)
                .GetConstructor(new[] {typeof(IEnumerable<object>[])})
                ?.Invoke(new object[] {m_Fields.Select(i => i.Get(target))});
        }

        /// <summary>
        ///     Applies the values in the <paramref name="valueObject" /> to every member of this group.
        /// </summary>
        /// <param name="target">Declaring class instance to set the field values on.</param>
        /// <param name="valueObject">Value object instance.</param>
        public void SetTyped(TDeclaring target, TValueObject valueObject)
        {
            var zipped = m_Fields.Zip(
                valueObject,
                (field, value) => new
                {
                    Field = field,
                    Value = value
                });
            foreach (var pair in zipped) pair.Field.Set(target, pair.Value);
        }

        public override string ToString()
        {
            return
                $"{typeof(TValueObject).Name} {DeclaringType?.Name}.[{string.Join(", ", Fields.Select(f => f.MemberInfo.Name))}])";
        }
    }

    /// <summary>
    ///     Type erased base class for field groups.
    /// </summary>
    public abstract class FieldAccessGroup : FieldBase
    {
        protected FieldAccessGroup(Type declaringType) : base(declaringType)
        {
        }

        [NotNull] public abstract IEnumerable<FieldAccess> Fields { get; }
    }
}