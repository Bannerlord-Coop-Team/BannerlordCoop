using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Serializers
{
    public interface ISerializationManager
    {
        byte[] Serialize(object value);
        object Deserialize(byte[] serializedData);
        T Deserialize<T>(byte[] serializedData);
        bool TrySerialize(object obj, out byte[] serializedData);

        bool TryDeserialize(byte[] serializedData, out object obj);
        bool TryDeserialize<T>(byte[] serializedData, out T obj);
    }
}
