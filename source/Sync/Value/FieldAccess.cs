using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Sync.Reflection;

namespace Sync.Value
{
    /// <summary>
    ///     Typed interface to access an arbitrary field.
    /// </summary>
    /// <typeparam name="TDeclaring">Type declaring the field.</typeparam>
    /// <typeparam name="TFieldType">Type of the field itself.</typeparam>
    public class FieldAccess<TDeclaring, TFieldType> : FieldAccess
    {
        public FieldAccess(FieldInfo fieldInfo) : base(typeof(TDeclaring), fieldInfo)
        {
            if (fieldInfo.GetUnderlyingType() != typeof(TFieldType))
                throw new ArgumentException("Unexpected underlying type.", nameof(fieldInfo));
        }

        public TFieldType GetTyped(TDeclaring target)
        {
            return (TFieldType) Get(target);
        }

        public void SetTyped(TDeclaring target, TFieldType value)
        {
            Set(target, value);
        }
    }

    /// <summary>
    ///     Type-erased interface to access an arbitrary field.
    /// </summary>
    public abstract class FieldAccess : FieldBase
    {
        [NotNull] public readonly FieldInfo FieldInfo;

        protected FieldAccess(Type declaringType, [NotNull] FieldInfo fieldInfo) : base(declaringType)
        {
            FieldInfo = fieldInfo;
            m_GetterLocal = InvokableFactory.CreateUntypedGetter<object>(fieldInfo);
            m_Setter = InvokableFactory.CreateUntypedSetter<object>(fieldInfo);
        }

        /// <inheritdoc />
        public override object Get([CanBeNull] object target)
        {
            return m_GetterLocal(target);
        }

        /// <inheritdoc />
        public override void Set(object target, object value)
        {
            if (target == null) return;

            m_Setter.Invoke(target, value);
        }

        public override string ToString()
        {
            return $"{DeclaringType?.Name}.{FieldInfo.Name}";
        }

        #region Private

        [NotNull] private readonly Func<object, object> m_GetterLocal;

        [NotNull] private readonly Action<object, object> m_Setter;

        #endregion
    }
}