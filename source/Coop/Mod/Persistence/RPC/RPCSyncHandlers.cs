using System.Collections.Generic;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    public class RPCSyncHandlers
    {
        private readonly List<MethodCallSyncHandler> m_Handlers = new List<MethodCallSyncHandler>();

        public IReadOnlyList<MethodCallSyncHandler> Handlers => m_Handlers;

        public void Register(MethodPatcher patcher)
        {
            foreach (MethodAccess syncMethod in patcher.Methods)
            {
                Register(syncMethod);
            }
        }

        public void Register(MethodAccess method)
        {
            m_Handlers.Add(new MethodCallSyncHandler(method));
        }
    }
}
