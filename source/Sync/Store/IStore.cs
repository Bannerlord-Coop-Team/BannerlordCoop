using System;
using System.Collections.Generic;

namespace Sync.Store
{
    public readonly struct ObjectId
    {
        /// <summary>
        /// Overrides casting to byte array
        /// </summary>
        /// <param name="id">Object to cast</param>
        public static implicit operator byte[](ObjectId id)
        {
            return BitConverter.GetBytes(id.Value);
        }

        public bool Equals(ObjectId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ObjectId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int) Value;
        }

        public uint Value { get; }

        public ObjectId(uint id)
        {
            Value = id;
        }

        public ObjectId(byte[] bytes)
        {
            Value = BitConverter.ToUInt32(bytes, 0);
        }

        public override string ToString()
        {
            return $"Obj {Value}";
        }

        public static bool operator ==(ObjectId lhs, ObjectId rhs)
        {
            return lhs.Value == rhs.Value;
        }

        public static bool operator !=(ObjectId lhs, ObjectId rhs)
        {
            return lhs.Value != rhs.Value;
        }
    }

    /// <summary>
    ///     Interface for a storage of arbitrary serializable data.
    /// </summary>
    public interface IStore
    {
        /// <summary>
        ///     Access the stored data.
        /// </summary>
        IReadOnlyDictionary<ObjectId, object> Data { get; }

        /// <summary>
        ///     Serialize an object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        byte[] Serialize(object obj);

        /// <summary>
        ///     Deserialize an object.
        /// </summary>
        /// <param name="raw"></param>
        /// <returns></returns>
        object Deserialize(byte[] raw);
        
        /// <summary>
        ///     Inserts an object into the store.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        ObjectId Insert(object obj);
        
        /// <summary>
        ///     Inserts an already serialized object into the store.
        /// </summary>
        /// <param name="obj">Object to insert</param>
        /// <param name="serialized">Serialized obj</param>
        /// <returns></returns>
        ObjectId Insert(object obj, byte[] serialized);
        
        /// <summary>
        ///     Removes an object from the store.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        bool Remove(ObjectId id);
    }
}