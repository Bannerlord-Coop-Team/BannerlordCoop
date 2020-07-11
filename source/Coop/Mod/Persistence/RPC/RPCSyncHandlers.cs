using System.Collections.Generic;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Manages a collection of <see cref="MethodCallSyncHandler" />.
    /// </summary>
    public class RPCSyncHandlers
    {
        private readonly List<MethodCallSyncHandler> m_Handlers = new List<MethodCallSyncHandler>();

        public IReadOnlyList<MethodCallSyncHandler> Handlers => m_Handlers;

        public void Register(IEnumerable<MethodAccess> patches, IClientAccess access)
        {
            foreach (MethodAccess syncMethod in patches)
            {
                Register(syncMethod, access);
            }
        }

        public void Register(MethodAccess method, IClientAccess access)
        {
            m_Handlers.Add(new MethodCallSyncHandler(method, access));
        }
    }
}
