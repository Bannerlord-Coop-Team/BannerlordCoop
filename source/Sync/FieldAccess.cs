using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Sync.Reflection;

namespace Sync
{
    /// <summary>
    ///     Typed interface to access an arbitrary field.
    /// </summary>
    /// <typeparam name="TDeclaring">Type declaring the field.</typeparam>
    /// <typeparam name="TFieldType">Type of the field itself.</typeparam>
    public class FieldAccess<TDeclaring, TFieldType> : FieldAccess
    {
        public FieldAccess(FieldInfo memberInfo) : base(memberInfo)
        {
            if (memberInfo.GetUnderlyingType() != typeof(TFieldType))
            {
                throw new ArgumentException("Unexpected underlying type.", nameof(memberInfo));
            }
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
    public abstract class FieldAccess : ValueAccess
    {
        [NotNull] private readonly Func<object, object> m_GetterLocal;
        [NotNull] private readonly FieldInfo m_MemberInfo;
        [NotNull] private readonly Action<object, object> m_Setter;

        protected FieldAccess([NotNull] FieldInfo memberInfo)
        {
            m_MemberInfo = memberInfo;
            m_GetterLocal = InvokableFactory.CreateUntypedGetter<object>(memberInfo);
            m_Setter = InvokableFactory.CreateUntypedSetter<object>(memberInfo);
        }

        /// <inheritdoc />
        public override Type GetDeclaringType()
        {
            return m_MemberInfo.DeclaringType;
        }

        /// <inheritdoc />
        public override object Get([CanBeNull] object target)
        {
            return m_GetterLocal(target);
        }

        /// <inheritdoc />
        public override void Set(object target, object value)
        {
            if (target == null)
            {
                return;
            }

            m_Setter.Invoke(target, value);
        }

        public override string ToString()
        {
            return $"{m_MemberInfo.DeclaringType?.Name}.{m_MemberInfo.Name}";
        }
    }
}
