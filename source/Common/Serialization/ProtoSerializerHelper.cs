using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Serialization
{
    public class ProtoSerializerHelper
    {
        public static byte[] Serialize(object obj)
        {
            if (Attribute.IsDefined(obj.GetType(), typeof(ProtoContractAttribute)))
            {
                using (var ms = new MemoryStream())
                {
                    Serializer.Serialize(ms, obj);
                    return ms.ToArray();
                }
            }

            throw new InvalidDataException("Object requested for serialization was not marked as ProtoContract");
        }

        public static T Deserialize<T>(byte[] data)
        {
            ReadOnlySpan<byte> bytes = data;

            return Serializer.Deserialize<T>(bytes);
        }
    }
}
