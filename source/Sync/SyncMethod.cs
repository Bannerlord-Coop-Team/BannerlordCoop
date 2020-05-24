using JetBrains.Annotations;
using Sync.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sync
{
    public class SyncMethod : IWatchable
    {
        private readonly Action<object, object[]> m_Call;
        public SyncMethod(MethodInfo info)
        {
            m_Call = InvokableFactory.CreateCall<object, object[]>(info);
        }

        public void Invoke([CanBeNull] object target, [CanBeNull] object[] args)
        {
            m_Call.Invoke(target, args);
        }

        private readonly Dictionary<object, Action<object>> m_SyncHandlers =
            new Dictionary<object, Action<object>>();
        [CanBeNull]
        public Action<object> GetSyncHandler([NotNull] object syncableInstance)
        {
            return m_SyncHandlers.TryGetValue(syncableInstance, out Action<object> handler) ?
                handler :
                null;
        }
        public void RemoveSyncHandler([NotNull] object syncableInstance)
        {
             m_SyncHandlers.Remove(syncableInstance);
        }

        public void SetSyncHandler([NotNull] object syncableInstance, Action<object> action)
        {
            if (m_SyncHandlers.ContainsKey(syncableInstance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_SyncHandlers.Add(syncableInstance, action);
        }
    }
}
