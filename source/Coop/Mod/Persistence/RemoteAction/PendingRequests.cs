using System;
using System.Collections.Generic;
using RemoteAction;

namespace Coop.Mod.Persistence.RemoteAction
{
    public class PendingRequests
    {
        private static readonly Lazy<PendingRequests> m_Instance =
            new Lazy<PendingRequests>(() => new PendingRequests());

        private readonly Dictionary<int, ISynchronizedAction> m_Pending = new Dictionary<int, ISynchronizedAction>();
        public static PendingRequests Instance => m_Instance.Value;

        public void Add(ISynchronizedAction action)
        {
            m_Pending.Add(action.GetHashCode(), action);
        }

        public bool IsPending(ISynchronizedAction action)
        {
            return m_Pending.ContainsKey(action.GetHashCode());
        }

        public void Remove(ISynchronizedAction action)
        {
            m_Pending.Remove(action.GetHashCode());
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