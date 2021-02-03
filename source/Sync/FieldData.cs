using JetBrains.Annotations;

namespace Sync
{
    public class FieldData
    {
        public FieldData([NotNull] FieldAccess access, object target, object value)
        {
            Access = access;
            Target = target;
            Value = value;
        }

        public FieldAccess Access { get; }
        public object Target { get; }
        public object Value { get; }
    }
}
