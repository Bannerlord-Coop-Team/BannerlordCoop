using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Util;

public static class InterfaceCollector
{
    public static IEnumerable<Type> GetInterfaces<T>(string @namespace)
    {
        return AppDomain.CurrentDomain.GetDomainTypes(@namespace)
            .Where(t => typeof(T).IsAssignableFrom(t) &&
                        t.IsConcrete());
    }

    public static bool IsConcrete(this Type type)
    {
        return type.IsClass &&
               type.IsGenericType == false &&
               type.IsAbstract == false;
    }
}
