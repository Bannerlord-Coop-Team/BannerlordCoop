using Common.Extensions;
using Common.Messaging;
using Common.PacketHandlers;
using System;
using System.Collections.Generic;

namespace Coop.Core.Common;

/// <summary>
/// Helper class that collects of all classes that inherit
/// <see cref="IHandler"/> or <see cref="IPacketHandler"/> interfaces
/// in a given namespace
/// </summary>
public static class TypeCollector
{
    /// <summary>
    /// collects of classes in a given namespace
    /// </summary>
    /// <typeparam name="TModule">Module type to get namespace</typeparam>
    /// <returns>
    /// Enumerable of types that inherit
    /// <typeparamref name="T"/>
    /// within the <typeparamref name="TModule"/>'s namespace
    /// </returns>
    public static IEnumerable<Type> Collect<TModule, T>()
    {
        string namespacePrefix = typeof(TModule).Namespace;

        List<Type> types = new List<Type>();

        var targetType = typeof(T);

        foreach (Type t in AppDomain.CurrentDomain.GetDomainTypes(namespacePrefix))
        {
            if (t.IsAbstract) continue;

            if (targetType.IsAssignableFrom(t) == false) continue;

            types.Add(t);
        }

        return types;
    }
}