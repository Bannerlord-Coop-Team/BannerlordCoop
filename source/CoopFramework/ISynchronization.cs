using System;
using System.Collections.Generic;
using RemoteAction;
using Sync;

namespace CoopFramework
{
    public interface ISynchronization
    {
        void Broadcast(MethodId id, object instance, object[] args);

        void RegisterSyncedField(ValueAccess value,
            IEnumerable<MethodAccess> triggers,
            Func<bool> condition);
    }
}