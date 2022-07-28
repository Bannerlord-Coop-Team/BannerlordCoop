using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Serialization
{
    [ProtoContract]
    internal readonly struct ProtoMessageWrapper
    {
        [ProtoMember(1)]
        public Type Type { get; }
        [ProtoMember(2)]
        public byte[] ContractData { get; }

        public ProtoMessageWrapper(Type type, byte[] contractData)
        {
            Type = type;
            ContractData = contractData;
        }
    }

    internal class ProtoBufSerializer : ISerializer
    {
        public Enum Protocol => DefaultProtocol.ProtoBuf;

        public object Deserialize(byte[] data)
        {
            using(var ms = new MemoryStream(data))
            {
                ProtoMessageWrapper wrapper = Serializer.Deserialize<ProtoMessageWrapper>(ms);
                using (var internalStream = new MemoryStream(wrapper.ContractData))
                {
                    return Serializer.Deserialize(wrapper.Type, internalStream);
                }
            }
        }

        private static MemoryStream WrapperStream = new MemoryStream();
        private static MemoryStream InternalStream = new MemoryStream();
        public byte[] Serialize(object obj)
        {
            using (WrapperStream)
            {
                Serializer.Serialize(WrapperStream, obj);
                ProtoMessageWrapper wrapper = new ProtoMessageWrapper(obj.GetType(), WrapperStream.ToArray());

                using (InternalStream)
                {
                    Serializer.Serialize(InternalStream, wrapper);
                    return InternalStream.ToArray();
                }
            }
        }
    }
}
