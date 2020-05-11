using JetBrains.Annotations;

namespace Coop.Sync
{
    public class FieldData
    {
        public FieldData([NotNull] Field field, object target, object value)
        {
            Field = field;
            Target = target;
            Value = value;
        }

        public Field Field { get; }
        public object Target { get; }
        public object Value { get; }
    }
}
