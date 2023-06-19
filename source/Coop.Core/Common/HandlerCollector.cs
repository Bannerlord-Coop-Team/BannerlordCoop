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
public static class HandlerCollector
{
    /// <summary>
    /// collects of classes in a given namespace
    /// </summary>
    /// <typeparam name="TModule">Module type to get namespace</typeparam>
    /// <returns>
    /// Enumerable of types that inherit
    /// <see cref="IHandler"/> or <see cref="IPacketHandler"/>
    /// within the <typeparamref name="TModule"/>'s namespace
    /// </returns>
    public static IEnumerable<Type> Collect<TModule>()
    {
        string namespacePrefix = typeof(TModule).Namespace;

        List<Type> types = new List<Type>();

        foreach (Type t in AppDomain.CurrentDomain.GetDomainTypes(namespacePrefix))
        {
            if (t.IsAbstract) continue;

            if (t.GetInterface(nameof(IHandler)) == null &&
                t.GetInterface(nameof(IPacketHandler)) == null) continue;

            types.Add(t);
        }

        return types;
    }
}