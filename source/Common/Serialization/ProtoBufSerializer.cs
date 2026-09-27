using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Buffers;
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
    // Fits most packets in one rent; larger payloads grow by doubling.
    private const int InitialPayloadBytes = 4096;
    // ProtoMessageWrapper tags: field 1 as a varint, field 2 length-delimited.
    private const byte TypeIdTag = 0x08;
    private const byte DataTag = 0x12;

    private readonly ISerializableTypeMapper typeMapper;
    private readonly ArrayPool<byte> bufferPool;

    // Proton reports Windows, so its managed runtime is the reliable discriminator.
    public static bool IsMonoRuntime { get; } = Type.GetType("Mono.Runtime") != null;
    public static bool AutoCompileEnabled => RuntimeTypeModel.Default.AutoCompile;
    public static bool StructFactoryWorkaroundEnabled => IsMonoRuntime;

    public static void ConfigureRuntimeModel()
    {
        ConfigureRuntimeModel(RuntimeTypeModel.Default, IsMonoRuntime);
    }

    internal static void ConfigureRuntimeModel(RuntimeTypeModel model, bool isMonoRuntime)
    {
        model.AfterApplyDefaultBehaviour -= ConfigureMonoValueType;
        if (isMonoRuntime) model.AfterApplyDefaultBehaviour += ConfigureMonoValueType;
        model.AutoCompile = true;
    }

    private static void ConfigureMonoValueType(object sender, TypeAddedEventArgs args)
    {
        if (!args.Type.IsValueType || args.MetaType.UseConstructor) return;

        // Wine-Mono rejects protobuf-net's uninitialized-object factory IL for value types.
        // Its no-factory path returns the same zero-initialized default value.
        args.MetaType.UseConstructor = true;
    }

    public ProtoBufSerializer(ISerializableTypeMapper typeMapper)
        : this(typeMapper, ArrayPool<byte>.Shared)
    {
    }

    internal ProtoBufSerializer(ISerializableTypeMapper typeMapper, ArrayPool<byte> bufferPool)
    {
        this.typeMapper = typeMapper;
        this.bufferPool = bufferPool;
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

    public byte[] Serialize(object obj)
    {
        if (typeMapper.TryGetId(obj.GetType(), out int typeId) == false)
        {
            throw new InvalidOperationException($"Type {obj.GetType().FullName} is not registered with the serialization type mapper");
        }
        
        using (var payload = new PooledWriteStream(InitialPayloadBytes, bufferPool))
        {
            RuntimeTypeModel.Default.Serialize(payload, obj);
            return WrapPayload(typeId, payload);
        }
    }

    // Writes the bytes protobuf-net writes for ProtoMessageWrapper, with a single copy of the payload.
    private static byte[] WrapPayload(int typeId, PooledWriteStream payload)
    {
        int payloadLength = payload.WrittenLength;
        // Negative ids sign-extend to ten bytes, as protobuf-net writes an int32 varint.
        ulong typeIdBits = (ulong)(long)typeId;
        // The default id 0 is omitted; Data is never null, so field 2 is always written.
        int typeIdLength = typeId == 0 ? 0 : 1 + GetVarintLength(typeIdBits);
        int headerLength = typeIdLength + 1 + GetVarintLength((ulong)payloadLength);

        var result = new byte[headerLength + payloadLength];
        int offset = 0;
        if (typeId != 0)
        {
            result[offset++] = TypeIdTag;
            offset = WriteVarint(result, offset, typeIdBits);
        }
        result[offset++] = DataTag;
        offset = WriteVarint(result, offset, (ulong)payloadLength);
        payload.CopyWrittenTo(result, offset);
        return result;
    }

    private static int GetVarintLength(ulong value)
    {
        int length = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            length++;
        }
        return length;
    }

    private static int WriteVarint(byte[] destination, int offset, ulong value)
    {
        while (value >= 0x80)
        {
            destination[offset++] = (byte)(value | 0x80);
            value >>= 7;
        }
        destination[offset++] = (byte)value;
        return offset;
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
