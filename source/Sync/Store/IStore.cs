using JetBrains.Annotations;
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
    ///     Interface for a shared object storage with a client  server model. It is intended for 
    ///     short term storage. Object lifetime in the store is controlled by the server.
    ///     
    ///     Data can be accessed through <see cref="Retrieve(ObjectId)"/>. Be aware that accessing
    ///     data informs the server about the access. The server may decide to remove the object 
    ///     from the store.
    /// </summary>
    public interface IStore
    {
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
        ///     Inserts an object into the store.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        ObjectId Insert(object obj, byte[] serialized);

        /// <summary>
        ///     Access an object. Be aware that this is communicated to the server. The server
        ///     may decide to remove the object after the client has accessed it.
        /// </summary>
        /// <param name="id"></param>
        [CanBeNull] object Retrieve(ObjectId id);
    }
}