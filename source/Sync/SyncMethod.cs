using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using Sync.Reflection;

namespace Sync
{
    public class SyncMethod : IWatchable
    {
        private readonly Action<object, object[]> m_Call;

        private readonly DynamicMethod m_StandIn;

        private readonly Dictionary<object, Action<object>> m_SyncHandlers =
            new Dictionary<object, Action<object>>();

        public SyncMethod([NotNull] MethodInfo info)
        {
            MemberInfo = info;
            m_StandIn = InvokableFactory.CreateStandIn(this);
            m_Call = InvokableFactory.CreateStandInCaller(m_StandIn);
            MethodRegistry.Register(this);
        }

        public MethodInfo MemberInfo { get; }

        public Action<object> GetSyncHandler(object syncableInstance)
        {
            return m_SyncHandlers.TryGetValue(syncableInstance, out Action<object> handler) ?
                handler :
                null;
        }

        public void RemoveSyncHandler(object syncableInstance)
        {
            m_SyncHandlers.Remove(syncableInstance);
        }

        public void SetSyncHandler(object syncableInstance, Action<object> action)
        {
            if (m_SyncHandlers.ContainsKey(syncableInstance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_SyncHandlers.Add(syncableInstance, action);
        }

        public void CallOriginal([CanBeNull] object target, [CanBeNull] object[] args)
        {
            m_Call.Invoke(target, args);
        }

        public override string ToString()
        {
            return $"{MemberInfo.DeclaringType?.Name}.{MemberInfo.Name}";
        }
    }
}
