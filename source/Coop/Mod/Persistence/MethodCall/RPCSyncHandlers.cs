using System.Collections.Generic;
using Sync;

namespace Coop.Mod.Persistence.MethodCall
{
    /// <summary>
    ///     Manages a collection of <see cref="MethodCallSyncHandler" />.
    /// </summary>
    public class RPCSyncHandlers
    {
        public List<MethodCallSyncHandler> Handlers { get; } = new List<MethodCallSyncHandler>();

        public void Register(IEnumerable<MethodAccess> patches, IClientAccess access)
        {
            foreach (MethodAccess syncMethod in patches)
            {
                Register(syncMethod, access);
            }
        }

        public void Register(MethodAccess method, IClientAccess access)
        {
            Handlers.Add(new MethodCallSyncHandler(method, access));
        }
    }
}
