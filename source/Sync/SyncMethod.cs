using System;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using Sync.Reflection;

namespace Sync
{
    public class SyncMethod : Watchable
    {
        [CanBeNull] private readonly Action<object, object[]> m_Call;
        [CanBeNull] private readonly Action<object[]> m_CallStatic;

        private readonly DynamicMethod m_StandIn;

        public SyncMethod([NotNull] MethodInfo info)
        {
            MemberInfo = info;
            Id = MethodRegistry.Register(this);
            m_StandIn = InvokableFactory.CreateStandIn(this);
            if (MemberInfo.IsStatic)
            {
                m_CallStatic = InvokableFactory.CreateStaticStandInCaller(m_StandIn);
            }
            else
            {
                m_Call = InvokableFactory.CreateStandInCaller(m_StandIn);
            }
        }

        public MethodId Id { get; }

        public MethodInfo MemberInfo { get; }

        public void CallOriginal([CanBeNull] object target, [CanBeNull] object[] args)
        {
            m_Call?.Invoke(target, args);
        }

        public override string ToString()
        {
            return $"{MemberInfo.DeclaringType?.Name}.{MemberInfo.Name}";
        }
    }
}
