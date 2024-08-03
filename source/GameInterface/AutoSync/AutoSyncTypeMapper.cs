using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GameInterface.AutoSync;

public interface IAutoSyncTypeMapper
{
    void AddType(Type type);
    int GetId(Type type);
    Type GetType(int id);
}

internal class AutoSyncTypeMapper : IAutoSyncTypeMapper
{

    private readonly ConditionalWeakTable<Type, Wrapper> idMap = new();

    private Type[] metaTypes = Array.Empty<Type>();

    class Wrapper
    {
        public readonly int Id;

        public Wrapper(int id)
        {
            Id = id;
        }
    }

    private void Remap()
    {
        int counter = 0;
        List<Type> tempList = new List<Type>();
        foreach(var type in RuntimeTypeModel.Default.GetTypes())
        {
            if (type is MetaType metaType)
            {
                tempList.Add(metaType.Type);
                idMap.Add(metaType.Type, new Wrapper(counter++));
            }
            else
            {
                throw new InvalidOperationException($"Type was not {nameof(MetaType)}, instead got {type.GetType().Name}");
            }
        }
        
        metaTypes = tempList.ToArray();
        Array.Sort(metaTypes);
    }

    public void AddType(Type type)
    {
        if (RuntimeTypeModel.Default.CanSerializeBasicType(type))
        {
            var newList = metaTypes.ToList();

            newList.Add(type);
            idMap.Add(type, new Wrapper(metaTypes.Length));

            metaTypes = newList.ToArray();
            Array.Sort(metaTypes);

            return;
        }

        RuntimeTypeModel.Default.Add(type, true);

        Remap();
            
    }

    public Type GetType(int id)
    {
        if (id < 0 || id > metaTypes.Length)
            throw new ArgumentOutOfRangeException(nameof(id));

        return metaTypes[id];
    }

    public int GetId(Type type)
    {
        if (idMap.TryGetValue(type, out var wrapper) == false) throw new InvalidOperationException($"{type.Name} was not in map");

        return wrapper.Id;
    }
}
