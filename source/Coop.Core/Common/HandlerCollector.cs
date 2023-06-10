using Common.Extensions;
using Common.Messaging;
using Common.PacketHandlers;
using System;
using System.Collections.Generic;

namespace Coop.Core.Common;

public static class HandlerCollector
{
    public static IEnumerable<Type> Collect<TModule>()
    {
        string namespacePrefix = typeof(TModule).Namespace;

        List<Type> types = new List<Type>();

        foreach (Type t in AppDomain.CurrentDomain.GetDomainTypes(namespacePrefix))
        {
            if (t.GetInterface(nameof(IHandler)) == null &&
                t.GetInterface(nameof(IPacketHandler)) == null) continue;

            types.Add(t);
        }

        return types;
    }
}