using ProtoBuf;
using System;
using System.IO;

namespace Common.Serialization;

public interface ICommonSerializer
{
    T Deserialize<T>(byte[] data);
    object Deserialize(byte[] data);
    byte[] Serialize(object obj);
}

public class ProtoBufSerializer : ICommonSerializer
{
    private readonly ISerializableTypeMapper typeMapper;

    public ProtoBufSerializer(ISerializableTypeMapper typeMapper)
    {
        this.typeMapper = typeMapper;
    }

    public T Deserialize<T>(byte[] data)
    {
        return (T)Deserialize(data);
    }

    public object Deserialize(byte[] data)
    {
        using(var ms = new MemoryStream(data))
        {
            ProtoMessageWrapper wrapper = Serializer.Deserialize<ProtoMessageWrapper>(ms);

            using (var internalStream = new MemoryStream(wrapper.Data))
            {
                if (typeMapper.TryGetType(wrapper.TypeId, out Type type) == false) return null;
                return Serializer.NonGeneric.Deserialize(type, internalStream);
            }
        }
    }

    public byte[] Serialize(object obj)
    {
        if (typeMapper.TryGetId(obj.GetType(), out int typeId) == false) return null;
        
        using (MemoryStream memoryStream = new MemoryStream())
        {
            Serializer.Serialize(memoryStream, obj);
            var wrapper = new ProtoMessageWrapper(typeId, memoryStream.ToArray());
            using (MemoryStream internalStream = new MemoryStream())
            {
                Serializer.Serialize(internalStream, wrapper);
                return internalStream.ToArray();
            }
        }
    }

    [ProtoContract]
    internal readonly struct ProtoMessageWrapper
    {
        [ProtoMember(1)]
        public int TypeId { get; }
        [ProtoMember(2)]
        public byte[] Data { get; }

        public ProtoMessageWrapper(int typeId, byte[] data)
        {
            TypeId = typeId;
            Data = data;
        }
    }
}
