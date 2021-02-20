namespace Sync.Value
{
    public readonly struct FieldId
    {
        public int InternalValue { get; }

        private static int _nextId = 1;

        public static FieldId GetNextId()
        {
            return new FieldId(_nextId++);
        }

        public static FieldId Invalid { get; } = new FieldId(0);

        public FieldId(int id)
        {
            InternalValue = id;
        }

        public override string ToString()
        {
            return $"FieldId {InternalValue}";
        }
    }
}