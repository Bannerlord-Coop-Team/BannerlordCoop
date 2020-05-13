using System;
using System.Reflection;
using Coop.Reflection;
using NLog;

namespace Coop.Sync
{
    public class SyncField<TTarget, TValue> : SyncField
    {
        public SyncField(FieldInfo memberInfo) : base(memberInfo)
        {
            if (memberInfo.GetUnderlyingType() != typeof(TValue))
            {
                throw new ArgumentException("Unexpected underlying type.", nameof(memberInfo));
            }
        }

        public TValue GetTyped(TTarget target)
        {
            return (TValue) Get(target);
        }

        public void SetTyped(TTarget target, TValue value)
        {
            Set(target, value);
        }
    }

    public abstract class SyncField : ISyncable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Func<object, object> m_GetterLocal;
        private readonly FieldInfo m_MemberInfo;
        private readonly Action<object, object> m_Setter;

        protected SyncField(FieldInfo memberInfo)
        {
            m_MemberInfo = memberInfo;
            m_GetterLocal = InvokableFactory.CreateUntypedGetter<object>(memberInfo);
            m_Setter = InvokableFactory.CreateUntypedSetter<object>(memberInfo);
        }

        public Action<object> SyncHandler { get; set; }

        public object Get(object target)
        {
            return m_GetterLocal(target);
        }

        public void Set(object target, object value)
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
