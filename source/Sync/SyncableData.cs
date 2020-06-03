using JetBrains.Annotations;

namespace Sync
{
    public class SyncableData
    {
        public SyncableData([NotNull] ValueAccess syncable, object target, object value)
        {
            Syncable = syncable;
            Target = target;
            Value = value;
        }

        public ValueAccess Syncable { get; }
        public object Target { get; }
        public object Value { get; }
    }
}
