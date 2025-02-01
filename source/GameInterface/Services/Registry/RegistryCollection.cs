using Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TaleWorlds.Library;

namespace GameInterface.Services.Registry;

/// <summary>
/// Storage for all object registries
/// </summary>
internal interface IRegistryCollection : IEnumerable<IRegistry>
{
    IEnumerable<IRegistry> Registries { get; }

    IReadOnlyDictionary<Type, IRegistry> RegistryMap { get; }

    void AddRegistry(IRegistry registry);
    void RemoveRegistry(IRegistry registry);
}

/// <inheritdoc cref="IRegistryCollection"/>
internal class RegistryCollection : IRegistryCollection
{
    public IEnumerable<IRegistry> Registries => registries.AsReadOnly();
    public IReadOnlyDictionary<Type, IRegistry> RegistryMap => registryMap.GetReadOnlyDictionary();

    private readonly List<IRegistry> registries = new List<IRegistry>();
    private readonly Dictionary<Type, IRegistry> registryMap = new Dictionary<Type, IRegistry>();

    public void AddRegistry(IRegistry registry)
    {
        registries.Add(registry);

        foreach (var type in registry.ManagedTypes)
        {
            registryMap.Add(type, registry);
        }
    }

    public void RemoveRegistry(IRegistry registry)
    {
        registries.Remove(registry);

        foreach (var type in registry.ManagedTypes)
        {
            registryMap.Remove(type);
        }
    }

    public IEnumerator<IRegistry> GetEnumerator() => Registries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Registries.GetEnumerator();
}
