using Common.Logging;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common.Serialization;

/// <summary>
/// Converts types to and from an integer id used on the wire.
/// </summary>
public interface ISerializableTypeMapper
{
    /// <summary>
    /// Registers types with the mapper.
    /// </summary>
    /// <param name="types">Types to add</param>
    /// <remarks>
    /// Each id is derived from a stable hash of the type's full name, so a given type always maps to
    /// the same id regardless of which other types a node has registered, in what order, or which
    /// assemblies it has loaded. Two processes therefore agree on the wire id even when their loaded
    /// type sets differ (e.g. the client loads Missions.dll and the server does not).
    /// </remarks>
    void AddTypes(IEnumerable<Type> types);

    /// <summary>
    /// Tries to get the id from a type.
    /// </summary>
    bool TryGetId(Type type, out int id);

    /// <summary>
    /// Tries to get the type from an id.
    /// </summary>
    bool TryGetType(int id, out Type type);
}

/// <inheritdoc cref="ISerializableTypeMapper"/>
public class SerializableTypeMapper : ISerializableTypeMapper
{
    private static readonly ILogger Logger = LogManager.GetLogger<SerializableTypeMapper>();

    private readonly Dictionary<int, Type> idToType = new Dictionary<int, Type>();
    private readonly Dictionary<string, int> fullNameToId = new Dictionary<string, int>();

    // Number of loaded assemblies at the last scan, so TryGetType only rescans when new ones appear.
    private int lastScannedAssemblyCount;

    public SerializableTypeMapper()
    {
        CollectProtoContracts();
    }

    private void CollectProtoContracts()
    {
        lastScannedAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;

        // Get all types with the ProtoContract attribute
        var serializableTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.IsDynamic == false)
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(type => {
                try
                {
                    return type.IsDefined(typeof(ProtoContractAttribute), inherit: false);
                }
                // Some types have malformed attributes?
                catch (CustomAttributeFormatException)
                {
                    return false;
                }
            });

        AddTypes(serializableTypes);
    }

    public void AddTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            Register(type);
        }
    }

    private void Register(Type type)
    {
        var fullName = type.FullName;
        // Open generics / type parameters have no stable full name; they are never serialized directly.
        if (fullName == null) return;
        if (fullNameToId.ContainsKey(fullName)) return;

        int id = StableId(fullName);

        if (idToType.TryGetValue(id, out var existing) && existing != type)
        {
            // Two distinct types cannot share an id. This is deterministic across nodes (both compute
            // the same hashes), so surface it at registration — build/startup — instead of letting it
            // silently corrupt the wire. If it ever fires, rename a type or widen the hash to 64 bits.
            var message = $"Serialization type id collision: '{existing.FullName}' and '{fullName}' both hash to {id}";
            Logger.Error(message);
            throw new InvalidOperationException(message);
        }

        idToType[id] = type;
        fullNameToId[fullName] = id;
    }

    public bool TryGetType(int id, out Type type)
    {
        if (idToType.TryGetValue(id, out type)) return true;

        // The id may belong to a type from an assembly loaded after the mapper was built (e.g.
        // Missions.dll on the server, which loads lazily on a mission event). Rescan once if new
        // assemblies have appeared since the last scan, then retry.
        if (AppDomain.CurrentDomain.GetAssemblies().Length != lastScannedAssemblyCount)
        {
            CollectProtoContracts();
            return idToType.TryGetValue(id, out type);
        }

        Logger.Error("No type is registered for serialization id {id}.", id);
        return false;
    }

    public bool TryGetId(Type type, out int id)
    {
        id = 0;
        var fullName = type.FullName;
        if (fullName == null)
        {
            Logger.Error("Type {typeName} has no full name and cannot be serialized.", type);
            return false;
        }

        // Register on demand. The wire id is a pure function of the full name, so a type whose
        // assembly loaded after the mapper was built still gets the same id every node computes for
        // it — no need to have pre-collected it at startup.
        if (!fullNameToId.TryGetValue(fullName, out id))
        {
            Register(type);
            id = fullNameToId[fullName];
        }

        return true;
    }

    /// <summary>
    /// Deterministic 32-bit FNV-1a hash of the type's full name, masked to a non-negative int so it
    /// encodes compactly as a protobuf varint. Must NOT use <see cref="string.GetHashCode()"/>, which
    /// is randomized per process on .NET Core and would reintroduce client/server id divergence.
    /// </summary>
    private static int StableId(string fullName)
    {
        const uint prime = 16777619;
        uint hash = 2166136261;

        foreach (char c in fullName)
        {
            hash = (hash ^ (byte)c) * prime;
            hash = (hash ^ (byte)(c >> 8)) * prime;
        }

        return (int)(hash & 0x7FFFFFFF);
    }
}
