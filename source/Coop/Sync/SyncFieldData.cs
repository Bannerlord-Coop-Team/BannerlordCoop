using JetBrains.Annotations;

namespace Coop.Sync
{
    public class SyncFieldData
    {
        public SyncFieldData([NotNull] SyncField field, object target, object value)
        {
            Field = field;
            Target = target;
            Value = value;
        }

        public SyncField Field { get; }
        public object Target { get; }
        public object Value { get; }
    }
}
