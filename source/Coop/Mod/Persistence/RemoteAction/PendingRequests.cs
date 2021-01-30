using System;
using System.Collections.Generic;
using RemoteAction;

namespace Coop.Mod.Persistence.RemoteAction
{
    public class PendingRequests
    {
        private static readonly Lazy<PendingRequests> m_Instance =
            new Lazy<PendingRequests>(() => new PendingRequests());

        private readonly Dictionary<int, IAction> m_Pending = new Dictionary<int, IAction>();
        public static PendingRequests Instance => m_Instance.Value;

        public void Add(IAction action)
        {
            m_Pending.Add(action.GetHashCode(), action);
        }

        public bool IsPending(IAction action)
        {
            return m_Pending.ContainsKey(action.GetHashCode());
        }

        public void Remove(IAction action)
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