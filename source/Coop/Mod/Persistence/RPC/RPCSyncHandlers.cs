using System.Collections.Generic;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    public class RPCSyncHandlers
    {
        private readonly List<MethodCallSyncHandler> m_Handlers = new List<MethodCallSyncHandler>();

        public IReadOnlyList<MethodCallSyncHandler> Handlers => m_Handlers;

        public void Register(MethodPatch patch)
        {
            foreach (MethodAccess syncMethod in patch.Methods)
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
