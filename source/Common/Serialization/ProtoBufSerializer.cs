using ProtoBuf;
using System;
using System.IO;

namespace Common.Serialization
{
    [ProtoContract]
    internal readonly struct ProtoMessageWrapper
    {
        public Type Type { get => Type.GetType(type); }
        [ProtoMember(1)]
        private readonly string type;
        [ProtoMember(2)]
        public byte[] ContractData { get; }

        public ProtoMessageWrapper(Type type, byte[] contractData)
        {
            this.type = type.AssemblyQualifiedName;
            ContractData = contractData;
        }
    }

    public class ProtoBufSerializer
    {
        public static object Deserialize(byte[] data)
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

        public static byte[] Serialize(object obj)
        {
            using (MemoryStream WrapperStream = new MemoryStream())
            {
                Serializer.Serialize(WrapperStream, obj);
                var wrapper = new ProtoMessageWrapper(obj.GetType(), WrapperStream.ToArray());
                using (MemoryStream InternalStream = new MemoryStream())
                {
                    Serializer.Serialize(InternalStream, wrapper);
                    return InternalStream.ToArray();
                }
            }
        }
    }
}
