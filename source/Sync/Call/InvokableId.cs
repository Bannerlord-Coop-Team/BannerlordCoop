using System.Threading;

namespace Sync.Call
{
    /// <summary>
    ///     An ID of a callable object that is known to <see cref="Sync"/>. 
    /// </summary>
    public readonly struct InvokableId
    {
        /// <summary>
        ///     The integer value of the id.
        /// </summary>
        public int InternalValue { get; }
        /// <summary>
        ///     Returns a new ID that has not been used so far.
        /// </summary>
        /// <returns></returns>
        public static InvokableId CreateUnique()
        {
            return new InvokableId(Interlocked.Increment(ref _nextId));
        }
        public static InvokableId Invalid { get; } = new InvokableId(-1);
        public InvokableId(int id)
        {
            InternalValue = id;
        }
        public override string ToString()
        {
            return $"InvokableId {InternalValue}";
        }
        private static int _nextId = 0;
    }
}