using System;
using System.Collections.Generic;
using RemoteAction;
using Sync;

namespace CoopFramework
{
    public interface ISynchronization
    {
        void Broadcast(MethodCall call);

        void RegisterSyncedField(ValueAccess value,
            IEnumerable<MethodAccess> triggers,
            Func<bool> condition);
    }
}