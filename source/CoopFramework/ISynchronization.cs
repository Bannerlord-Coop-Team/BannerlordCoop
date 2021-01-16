using System;
using System.Collections.Generic;
using Sync;

namespace CoopFramework
{
    public interface ISynchronization
    {
        void Broadcast(MethodAccess access);

        void RegisterSyncedField(ValueAccess value,
            IEnumerable<MethodAccess> triggers,
            Func<bool> condition);
    }
}