using Common;
using System.Collections;
using System.Collections.Generic;

namespace GameInterface.Services.Registry;

/// <summary>
/// Storage for all object registries
/// </summary>
internal interface IRegistryCollection : IEnumerable<IRegistry>
{
    IEnumerable<IRegistry> Registries { get; }

    void AddRegistry(IRegistry registry);
    void RemoveRegistry(IRegistry registry);
}

/// <inheritdoc cref="IRegistryCollection"/>
internal class RegistryCollection : IRegistryCollection
{
    public IEnumerable<IRegistry> Registries => registries.AsReadOnly();

    private readonly List<IRegistry> registries = new List<IRegistry>();

    public void AddRegistry(IRegistry registry)
    {
        registries.Add(registry);
    }

    public void RemoveRegistry(IRegistry registry)
    {
        registries.Remove(registry);
    }

    public IEnumerator<IRegistry> GetEnumerator() => Registries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Registries.GetEnumerator();
}
