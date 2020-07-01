using System.Collections.Generic;

namespace Sync.Store
{
    public struct ObjectId
    {
        public uint Value { get; }

        public ObjectId(uint id)
        {
            Value = id;
        }

        public override string ToString()
        {
            return $"Object {Value}";
        }
    }

    public interface IStore
    {
        IReadOnlyDictionary<ObjectId, object> Data { get; }
        ObjectId Insert(object obj);
        bool Remove(ObjectId id);
    }
}
