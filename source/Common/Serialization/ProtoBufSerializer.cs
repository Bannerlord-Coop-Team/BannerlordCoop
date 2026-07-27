using ProtoBuf;
using ProtoBuf.Meta;
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
