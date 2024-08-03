using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GameInterface.AutoSync;

public interface IAutoSyncPropertyMapper
{
    int AddPropertySetter(MethodInfo propertySetter);
    int GetId(MethodInfo propertySetter);
    MethodInfo GetSetter(int id);
}

internal class AutoSyncPropertyMapper : IAutoSyncPropertyMapper
{

    private MethodInfo[] propertyMap = Array.Empty<MethodInfo>();

    private readonly ConditionalWeakTable<MethodInfo, IdWrapper> methodToId = new();

    class IdWrapper
    {
        public int Id { get; }

        public IdWrapper(int id)
        {
            Id = id;
        }
    }


    private void Remap()
    {
        for (int i = 0; i < propertyMap.Length; i++)
        {
            methodToId.Add(propertyMap[i], new IdWrapper(i));
        }
    }

    public int AddPropertySetter(MethodInfo propertySetter)
    {
        propertyMap = propertyMap.AddItem(propertySetter).ToArray();
        Array.Sort(propertyMap);

        Remap();

        return GetId(propertySetter);
    }

    public MethodInfo GetSetter(int id)
    {
        if (id < 0 || id > propertyMap.Length)
            throw new ArgumentOutOfRangeException(nameof(id));

        return propertyMap[id];
    }

    public int GetId(MethodInfo propertySetter)
    {
        if (methodToId.TryGetValue(propertySetter, out var wrapper) == false) throw new InvalidOperationException($"{propertySetter.Name} was not in map");

        return wrapper.Id;
    }
}
