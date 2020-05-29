namespace Sync
{
    public struct MethodId
    {
        public int InternalValue { get; }

        private static readonly int _NextId = 1;

        public static MethodId GetNextId()
        {
            return new MethodId(_NextId);
        }

        public static MethodId Invalid { get; } = new MethodId(0);

        public MethodId(int id)
        {
            InternalValue = id;
        }
    }
}
