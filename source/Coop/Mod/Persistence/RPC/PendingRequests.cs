using System;
using System.Collections.Generic;

namespace Coop.Mod.Persistence.RPC
{
    public class PendingRequests
    {
        private static readonly Lazy<PendingRequests> m_Instance =
            new Lazy<PendingRequests>(() => new PendingRequests());
        public static PendingRequests Instance => m_Instance.Value;
        
        private readonly Dictionary<int, MethodCall> m_Pending = new Dictionary<int, MethodCall>();

        public void Add(MethodCall call)
        {
            m_Pending.Add(call.GetHashCode(), call);
        }

        public bool IsPending(MethodCall call)
        {
            return m_Pending.ContainsKey(call.GetHashCode());
        }

        public void Remove(MethodCall call)
        {
            m_Pending.Remove(call.GetHashCode());
        }

        public int PendingRequestCount()
        {
            return m_Pending.Count;
            
        }
    }
}
