using Common.Serialization;
using ProtoBuf;

namespace Common.Tests.Serialization;

/// <summary>
/// Frozen copy of <see cref="ProtoBufSerializer"/> before #3076, kept as the wire reference for equivalence tests.
/// </summary>
public sealed class LegacyProtoBufSerializer : ICommonSerializer
{
    private readonly ISerializableTypeMapper typeMapper;

    public LegacyProtoBufSerializer(ISerializableTypeMapper typeMapper)
    {
        this.typeMapper = typeMapper;
    }

    public T Deserialize<T>(byte[] data)
    {
        return (T)Deserialize(data);
    }

    public object Deserialize(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        {
            LegacyWrapper wrapper = Serializer.Deserialize<LegacyWrapper>(ms);

            using (var internalStream = new MemoryStream(wrapper.Data))
            {
                if (typeMapper.TryGetType(wrapper.TypeId, out Type type) == false) return null!;
                return Serializer.Deserialize(type, internalStream);
            }
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
            var wrapper = new LegacyWrapper(typeId, memoryStream.ToArray());
            using (MemoryStream internalStream = new MemoryStream())
            {
                Serializer.Serialize(internalStream, wrapper);
                return internalStream.ToArray();
            }
        }
    }

    /// <summary>Reads the wrapper with the frozen contract, without resolving the payload type.</summary>
    public static (int TypeId, byte[] Payload) ReadWrapper(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        {
            LegacyWrapper wrapper = Serializer.Deserialize<LegacyWrapper>(ms);
            return (wrapper.TypeId, wrapper.Data);
        }
    }

    // Same contract as ProtoBufSerializer.ProtoMessageWrapper, frozen so a wrapper change shows up as a byte diff.
    [ProtoContract]
    internal readonly struct LegacyWrapper
    {
        [ProtoMember(1)]
        public int TypeId { get; }
        [ProtoMember(2)]
        public byte[] Data { get; }

        public LegacyWrapper(int typeId, byte[] data)
        {
            TypeId = typeId;
            Data = data;
        }
    }
}
