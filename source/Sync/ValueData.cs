using JetBrains.Annotations;

namespace Sync
{
    public class ValueData
    {
        public ValueData([NotNull] ValueAccess access, object target, object value)
        {
            Access = access;
            Target = target;
            Value = value;
        }

        public ValueAccess Access { get; }
        public object Target { get; }
        public object Value { get; }
    }
}
