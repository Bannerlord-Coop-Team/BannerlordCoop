using JetBrains.Annotations;

namespace Sync.Value
{
    public class FieldData
    {
        public FieldData([NotNull] FieldBase field, object target, object value)
        {
            FieldBase = field;
            Target = target;
            Value = value;
        }

        public FieldBase FieldBase { get; }
        public object Target { get; }
        public object Value { get; }
    }
}