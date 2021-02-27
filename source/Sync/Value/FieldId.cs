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

        public override bool Equals(object obj)
        {
            if (obj is FieldId f)
            {
                return Equals(f);
            }

            return false;
        }

        public bool Equals(FieldId other)
        {
            return InternalValue == other.InternalValue;
        }

        public override int GetHashCode()
        {
            return InternalValue;
        }

        public static bool operator ==(FieldId f0, FieldId f1)
        {
            return f0.Equals(f1);
        }

        public static bool operator !=(FieldId f0, FieldId f1)
        {
            return !(f0 == f1);
        }
    }
}