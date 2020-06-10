using System.Collections.Generic;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    public class RPCSyncHandlers
    {
        private readonly List<MethodCallSyncHandler> m_Handlers = new List<MethodCallSyncHandler>();

        public IReadOnlyList<MethodCallSyncHandler> Handlers => m_Handlers;

        public void Register(IEnumerable<MethodAccess> patches)
        {
            foreach (MethodAccess syncMethod in patches)
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
