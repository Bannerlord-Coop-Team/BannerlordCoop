using JetBrains.Annotations;
using System.Collections.Generic;

namespace Sync.Store
{
    public readonly struct ObjectId
    {
        public uint Value { get; }

        public ObjectId(uint id)
        {
            Value = id;
        }

        public override string ToString()
        {
            return $"Obj {Value}";
        }

        public static bool operator ==(ObjectId lhs, ObjectId rhs) => lhs.Value == rhs.Value;
        public static bool operator !=(ObjectId lhs, ObjectId rhs) => lhs.Value != rhs.Value;
    }

    public interface IStore
    {
        IReadOnlyDictionary<ObjectId, object> Data { get; }
        ObjectId Insert(object obj);
        bool Remove(ObjectId id);
    }
}
