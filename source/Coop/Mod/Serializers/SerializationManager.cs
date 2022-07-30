using Common;
using Common.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Serializers
{
    public class SerializationManager : ISerializationManager
    {
        public object Deserialize(byte[] serializedData)
        {
            object deserializedObj = CommonSerializer.Deserialize(serializedData);
            if (typeof(ICustomSerializer).IsAssignableFrom(deserializedObj.GetType()))
            {
                return ((ICustomSerializer)deserializedObj).Deserialize();
            }
            return CommonSerializer.Deserialize(serializedData);
        }

        public T Deserialize<T>(byte[] serializedData)
        {
            return (T)Deserialize(serializedData);
        }

        public byte[] Serialize(object value)
        {
            return CommonSerializer.Serialize(value);
        }

        public bool TryDeserialize(byte[] serializedData, out object obj)
        {
            try
            {
                obj = CommonSerializer.Deserialize(serializedData);
                if (typeof(ICustomSerializer).IsAssignableFrom(obj.GetType()))
                {
                    obj = ((ICustomSerializer)obj).Deserialize();
                }
                return true;
            }
            catch (System.Runtime.Serialization.SerializationException) { }

            obj = null;
            return false;
        }

        public bool TryDeserialize<T>(byte[] serializedData, out T obj)
        {
            try
            {
                bool result = TryDeserialize(serializedData, out object obj2);
                obj = (T)obj2;
                return result;
            }
            catch (InvalidCastException) { }

            obj = default;
            return false;
        }

        public bool TrySerialize(object obj, out byte[] serializedData)
        {
            try
            {
                serializedData = CommonSerializer.Serialize(obj);
                return true;
            }
            catch (System.Runtime.Serialization.SerializationException) { }

            serializedData = null;
            return false;
        }
    }
}
