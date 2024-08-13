using Common.Logging;
using ProtoBuf;
using ProtoBuf.Meta;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common.Serialization;

/// <summary>
/// Converts types to and from an integer id.
/// </summary>
public interface ISerializableTypeMapper
{
    /// <summary>
    /// Adds types to type mapper
    /// </summary>
    /// <param name="types">Types to add</param>
    /// <remarks>
    /// Types should be added in the same order on both the client and server.
    /// </remarks>
    void AddTypes(IEnumerable<Type> types);

    /// <summary>
    /// Tries to get the id from a type.
    /// </summary>
    /// <param name="type">Type to attempt id retreival</param>
    /// <param name="id">id out parameter</param>
    /// <returns>True if successful otherwise false</returns>
    bool TryGetId(Type type, out int id);

    /// <summary>
    /// Tries to get the type from an id.
    /// </summary>
    /// <param name="id">Id to attempt type retreival</param>
    /// <param name="type">type out parameter</param>
    /// <returns>True if successful otherwise false</returns>
    bool TryGetType(int id, out Type type);
}

/// <inheritdoc cref="ISerializableTypeMapper"/>"
public class SerializableTypeMapper : ISerializableTypeMapper
{
    private static readonly ILogger Logger = LogManager.GetLogger<SerializableTypeMapper>();

    private Dictionary<string, int> AddedKeyData = new Dictionary<string, int>();
    private Type[] TypeMap = Array.Empty<Type>();

    public SerializableTypeMapper()
    {
        CollectProtoContracts();
    }

    private void CollectProtoContracts()
    {
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
            .Where(type =>
            {
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
        var sortedTypes = TypeMap.Concat(types).OrderBy(type => type.FullName).Distinct();

        TypeMap = sortedTypes.ToArray();
        AddedKeyData = sortedTypes
            .Select((type, index) => (type, index))
            .ToDictionary(pair => pair.type.FullName, pair => pair.index);
    }

    public bool TryGetType(int id, out Type type)
    {
        if (id < 0 || id >= TypeMap.Length)
        {
            Logger.Error("Type id {id} is out of range.", id);
            type = null;
            return false;
        }

        type = TypeMap[id];
        return true;
    }

    public bool TryGetId(Type type, out int id)
    {
        if (AddedKeyData.TryGetValue(type.FullName, out id) == false)
        {
            Logger.Error("Type {typeName} is not registered in the type mapper.", type.FullName);
            return false;
        }

        return true;
    }
}
