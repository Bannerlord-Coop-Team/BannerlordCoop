using System;
using System.Reflection;
using Coop.Reflection;
using NLog;

namespace Coop.Sync
{
    public class SyncField
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Func<object, object> m_GetterLocal;
        private readonly FieldInfo m_MemberInfo;
        private readonly Action<object, object> m_Setter;
        public Action<object> SyncHandler;

        public SyncField(FieldInfo memberInfo)
        {
            m_MemberInfo = memberInfo;
            m_GetterLocal = InvokableFactory.CreateUntypedGetter<object>(memberInfo);
            m_Setter = InvokableFactory.CreateUntypedSetter<object>(memberInfo);
        }

        public object Get(object target)
        {
            return m_GetterLocal(target);
        }

        public void Set(object target, object value)
        {
            if (target == null)
            {
                Logger.Debug("Apply {field} ignored because target is null.", this);
                return;
            }

            Logger.Trace("Set {field} in {target} to {value}.", this, target, value);
            m_Setter.Invoke(target, value);
        }

        public override string ToString()
        {
            return $"{m_MemberInfo.DeclaringType?.Name}.{m_MemberInfo.Name}";
        }
    }
}
