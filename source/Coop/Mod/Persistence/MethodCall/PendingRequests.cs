using System;
using System.Collections.Generic;
using RemoteAction;

namespace Coop.Mod.Persistence.MethodCall
{
    public class PendingRequests
    {
        private static readonly Lazy<PendingRequests> m_Instance =
            new Lazy<PendingRequests>(() => new PendingRequests());
        public static PendingRequests Instance => m_Instance.Value;
        
        private readonly Dictionary<int, RemoteAction.MethodCall> m_Pending = new Dictionary<int, RemoteAction.MethodCall>();

        public void Add(RemoteAction.MethodCall call)
        {
            m_Pending.Add(call.GetHashCode(), call);
        }

        public bool IsPending(RemoteAction.MethodCall call)
        {
            return m_Pending.ContainsKey(call.GetHashCode());
        }

        public void Remove(RemoteAction.MethodCall call)
        {
            m_Pending.Remove(call.GetHashCode());
        }

        public int PendingRequestCount()
        {
            return m_Pending.Count;
        }

        public void Clear()
        {
            m_Pending.Clear();
        }
    }
}
