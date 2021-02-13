namespace Sync
{
    public readonly struct ValueId
    {
        public int InternalValue { get; }

        private static int _nextId = 1;

        public static ValueId GetNextId()
        {
            return new ValueId(_nextId++);
        }

        public static ValueId Invalid { get; } = new ValueId(0);

        public ValueId(int id)
        {
            InternalValue = id;
        }

        public override string ToString()
        {
            return $"ValueId {InternalValue}";
        }
    }
}