using JetBrains.Annotations;

namespace Coop.Sync
{
    public class SyncableData
    {
        public SyncableData([NotNull] ISyncable syncable, object target, object value)
        {
            Syncable = syncable;
            Target = target;
            Value = value;
        }

        public ISyncable Syncable { get; }
        public object Target { get; }
        public object Value { get; }
    }
}
