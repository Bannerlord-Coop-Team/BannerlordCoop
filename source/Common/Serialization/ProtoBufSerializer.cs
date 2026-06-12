using ProtoBuf;
using System;
using System.IO;

namespace Common.Serialization;

public interface ICommonSerializer
{
    T Deserialize<T>(byte[] data);
    object Deserialize(byte[] data);
    byte[] Serialize(object obj);

    /// <summary>
    /// Resolves the runtime type of serialized data without materializing the payload
    /// object graph, so no surrogate conversions run.
    /// </summary>
    /// <returns>False when the data cannot be read or its type id is unknown</returns>
    bool TryPeekType(byte[] data, out Type type);
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
                return Serializer.Deserialize(type, internalStream);
            }
        }
    }

    public bool TryPeekType(byte[] data, out Type type)
    {
        type = null;

        try
        {
            using (var ms = new MemoryStream(data))
            {
                // Reading a contract that only declares the type id field skips the payload
                // field without materializing it
                var peek = Serializer.Deserialize<ProtoTypeIdPeek>(ms);
                return typeMapper.TryGetType(peek.TypeId, out type);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public byte[] Serialize(object obj)
    {
        if (typeMapper.TryGetId(obj.GetType(), out int typeId) == false)
        {
            throw new InvalidOperationException($"Type {obj.GetType().FullName} is not registered with the serialization type mapper");
        }
        
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

    [ProtoContract(SkipConstructor = true)]
    private readonly struct ProtoTypeIdPeek
    {
        [ProtoMember(1)]
        public readonly int TypeId;

        public ProtoTypeIdPeek(int typeId)
        {
            TypeId = typeId;
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
