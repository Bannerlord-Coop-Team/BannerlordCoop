using System.Threading;

namespace Sync.Value
{
    /// <summary>
    ///     An ID of a field known to <see cref="Sync"/>.
    /// </summary>
    public readonly struct FieldId
    {
        /// <summary>
        ///     The integer value of the id.
        /// </summary>
        public int InternalValue { get; }
        /// <summary>
        ///     Returns a new ID that has not been used so far.
        /// </summary>
        /// <returns></returns>
        public static FieldId CreateUnique()
        {
            return new FieldId(Interlocked.Increment(ref _nextId));
        }

        public static FieldId Invalid { get; } = new FieldId(-1);

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
        
        private static int _nextId = 0;
    }
}