using Common.Logging;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common.Serialization;

public interface ISerializableTypeMapper
{
    void AddTypes(IEnumerable<Type> types);
    bool TryGetId(Type type, out int id);
    bool TryGetType(int id, out Type type);
}

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
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => {
                try
                {
                    a.GetTypes();
                    return a.IsDynamic == false;
                } catch(ReflectionTypeLoadException)
                {
                    return false;
                }
             })
            .SelectMany(a => a.GetTypes())
            .Where(type => type.GetCustomAttribute<ProtoContractAttribute>() != null);

        AddTypes(types);
    }

    public void AddTypes(IEnumerable<Type> types)
    {
        var sortedTypes = TypeMap.Concat(types).OrderBy(type => type.FullName);

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
