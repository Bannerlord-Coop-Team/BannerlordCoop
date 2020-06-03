using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    /// <summary>
    ///     A collection of <see cref="FieldAccess" /> that are declared in the same class. All fields of
    ///     the group are manipulated as a single unit. The value of each individual field is stored in an
    ///     <typeparamref name="TValueObject"> instance.
    /// </summary>
    /// <typeparam name="TDeclaring">Class that declares all fields contained in this group.</typeparam>
    /// <typeparam name="TValueObject">
    ///     Class that can store the values for all fields. Needs to implement a
    ///     <see cref="TValueObject.#ctor(IEnumerable`object)" /> constructor.
    /// </typeparam>
    public class FieldAccessGroup<TDeclaring, TValueObject> : ValueAccess
        where TValueObject : class, IEnumerable<object>
    {
        [NotNull] private readonly List<FieldAccess> m_Fields;

        public FieldAccessGroup()
        {
            m_Fields = new List<FieldAccess>();
            Init();
        }

        public FieldAccessGroup([NotNull] List<FieldAccess> fields)
        {
            m_Fields = fields;
            Init();
        }

        private void Init()
        {
            ConstructorInfo constructor =
                typeof(TValueObject).GetConstructor(new[] {typeof(IEnumerable<object>[])});
            if (constructor == null)
            {
                throw new ArgumentException($"{typeof(TValueObject)} has no matching constructor.");
            }
        }

        /// <summary>
        ///     Adds a field to the collection.
        /// </summary>
        /// <param name="sFieldName">Name of the field.</param>
        /// <typeparam name="TFieldType">Type of the field.</typeparam>
        /// <returns>this</returns>
        /// <exception cref="Exception">If the field was not found.</exception>
        public FieldAccessGroup<TDeclaring, TValueObject> AddField<TFieldType>(string sFieldName)
        {
            FieldInfo info = AccessTools.Field(typeof(TDeclaring), sFieldName);
            if (info == null)
            {
                throw new Exception($"Field {typeof(TDeclaring)}.{sFieldName} not found.");
            }

            return AddField<TFieldType>(info);
        }

        public FieldAccessGroup<TDeclaring, TValueObject> AddField<TFieldType>(
            [NotNull] FieldInfo memberInfo)
        {
            return AddField(new FieldAccess<TDeclaring, TFieldType>(memberInfo));
        }

        public FieldAccessGroup<TDeclaring, TValueObject> AddField(
            [NotNull] FieldAccess fieldAccess)
        {
            m_Fields.Add(fieldAccess);
            return this;
        }

        /// <inheritdoc />
        public override object Get(object target)
        {
            if (target is TDeclaring castedTarget)
            {
                return GetTyped(castedTarget);
            }

            throw new ArgumentException(
                $"Invalid argument. Expected {typeof(TDeclaring)}. Got {target.GetType()}.",
                nameof(target));
        }

        /// <inheritdoc />
        public override void Set(object target, object value)
        {
            if (target is TDeclaring castedTarget && value is TValueObject castedValue)
            {
                SetTyped(castedTarget, castedValue);
            }
            else
            {
                throw new ArgumentException(
                    $"Invalid arguments. Expected {typeof(TDeclaring)}, {typeof(TValueObject)}. Got {target.GetType()}, {value.GetType()}.");
            }
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
            foreach (var pair in zipped)
            {
                pair.Field.Set(target, pair.Value);
            }
        }
    }
}
