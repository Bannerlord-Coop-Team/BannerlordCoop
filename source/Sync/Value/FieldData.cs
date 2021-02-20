using JetBrains.Annotations;

namespace Sync.Value
{
    public class FieldData
    {
        public FieldData([NotNull] Field access, object target, object value)
        {
            Access = access;
            Target = target;
            Value = value;
        }

        public Field Access { get; }
        public object Target { get; }
        public object Value { get; }
    }
}