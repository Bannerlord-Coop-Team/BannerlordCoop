using Common;

namespace Sync
{
    public readonly struct MethodId
    {
        public int InternalValue { get; }

        private static int _nextId = 1;

        public static MethodId GetNextId()
        {
            return new MethodId(_nextId++);
        }

        public static MethodId Invalid { get; } = new MethodId(0);

        public MethodId(int id)
        {
            InternalValue = id;
        }

        public override string ToString()
        {
            return $"MethodId {InternalValue}";
        }
    }
}
