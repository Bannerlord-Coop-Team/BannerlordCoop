using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Contracts registered on Mono that have not yet had a compilation attempt. Compilation is
    /// deferred rather than performed inside
    /// <see cref="RuntimeTypeModel.AfterApplyDefaultBehaviour"/> because compiling a contract also
    /// builds serializers for its nested types, which would run ahead of those nested types' own
    /// configuration and make protobuf-net throw "The type cannot be changed once a serializer has
    /// been generated for &lt;T&gt;".
    /// </summary>
    private static readonly List<MetaType> PendingCompilation = new List<MetaType>();

    internal static void ConfigureRuntimeModel(RuntimeTypeModel model, bool isMonoRuntime)
    {
        model.AfterApplyDefaultBehaviour -= ConfigureMonoValueType;
        if (isMonoRuntime) model.AfterApplyDefaultBehaviour += ConfigureMonoValueType;

        // Wine-Mono's JIT rejects some of the IL protobuf-net emits when it compiles a
        // TypeSerializer, throwing InvalidProgramException ("Invalid IL code in (wrapper
        // dynamic-method)") the first time such a contract is serialized. Repeatedly sent messages
        // survive it, because the failed attempt leaves the MetaType resolvable and the next send
        // succeeds on the non-compiled path. One-shot messages do not: NetworkTransferNewHero is
        // published exactly once, from CharacterCreationState.Handle_CharacterCreationFinished, so
        // a single throw leaves both peers waiting on "Join Coop Campaign" until the tunnel times
        // out. ConfigureMonoValueType only replaces the uninitialized-object factory; the compiled
        // serializer is still emitted and still rejected.
        //
        // Leaving auto-compile off keeps protobuf-net on its reflection path, which emits no IL.
        // Rather than accept that cost for every contract, TryCompilePending gives each type one
        // opportunistic compilation attempt: whatever this runtime accepts keeps the emitted fast
        // path and only the contracts it rejects stay interpreted. If a runtime rejects all of
        // them, behaviour is exactly that of a plain AutoCompile=false.
        model.AutoCompile = !isMonoRuntime;
    }

    private static void ConfigureMonoValueType(object sender, TypeAddedEventArgs args)
    {
        lock (PendingCompilation) PendingCompilation.Add(args.MetaType);

        if (!args.Type.IsValueType || args.MetaType.UseConstructor) return;

        try
        {
            // Wine-Mono rejects protobuf-net's uninitialized-object factory IL for value types.
            // Its no-factory path returns the same zero-initialized default value.
            args.MetaType.UseConstructor = true;
        }
        catch (InvalidOperationException)
        {
            // A containing contract was compiled first and has already generated a serializer for
            // this type, so its factory choice is fixed and there is nothing left to change.
        }
    }

    /// <summary>
    /// Gives each newly registered contract one attempt at the compiled serializer. Runs before
    /// serializing rather than during registration so the model has settled first. A rejection is
    /// swallowed and leaves that contract on protobuf-net's reflection path, which is slower but
    /// emits no IL and is therefore always accepted.
    /// </summary>
    internal static void TryCompilePending()
    {
        MetaType[] pending;
        lock (PendingCompilation)
        {
            if (PendingCompilation.Count == 0) return;
            pending = PendingCompilation.ToArray();
            PendingCompilation.Clear();
        }

        for (var index = 0; index < pending.Length; index++)
        {
            try
            {
                pending[index].CompileInPlace();
            }
            catch
            {
                // This runtime rejected the emitted IL for this contract; the interpreted
                // serializer stays in place and remains fully functional.
            }
        }
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
        if (IsMonoRuntime) TryCompilePending();

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

        if (IsMonoRuntime) TryCompilePending();
        
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
