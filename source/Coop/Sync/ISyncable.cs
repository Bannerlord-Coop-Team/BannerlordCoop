using System;

namespace Coop.Sync
{
    public interface ISyncable
    {
        Action<object> SyncHandler { get; }
        object Get(object target);
        void Set(object target, object value);
    }
}
