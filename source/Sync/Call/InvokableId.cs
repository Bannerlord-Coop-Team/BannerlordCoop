namespace Sync.Call
{
    public readonly struct InvokableId
    {
        public int InternalValue { get; }

        private static int _nextId = 1;

        public static InvokableId GetNextId()
        {
            return new InvokableId(_nextId++);
        }

        public static InvokableId Invalid { get; } = new InvokableId(0);

        public InvokableId(int id)
        {
            InternalValue = id;
        }

        public override string ToString()
        {
            return $"InvokableId {InternalValue}";
        }
    }
}