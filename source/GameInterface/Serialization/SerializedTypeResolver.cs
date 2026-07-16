using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace GameInterface.Serialization;

internal static class SerializedTypeResolver
{
    public static Type ResolveLoadedType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            throw new SerializationException("Serialized type name was empty");

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        Type type = Type.GetType(
            typeName,
            requested => assemblies.FirstOrDefault(assembly => assembly.FullName == requested.FullName),
            (assembly, name, ignoreCase) => assembly?.GetType(name, false, ignoreCase) ??
                assemblies.Select(candidate => candidate.GetType(name, false, ignoreCase))
                    .FirstOrDefault(candidate => candidate != null),
            throwOnError: false,
            ignoreCase: false);

        if (type == null)
            throw new SerializationException($"Serialized type {typeName} is not already loaded");

        return type;
    }

    public static Type ResolveEnumerableType(string typeName)
    {
        Type type = ResolveLoadedType(typeName);
        if (type.IsArray && type.GetArrayRank() == 1) return type;
        if (type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(List<>) ||
             type.GetGenericTypeDefinition() == typeof(HashSet<>))) return type;

        throw new SerializationException($"Serialized enumerable type {type} is not allowed");
    }

    public static Type ResolveDictionaryType(string typeName)
    {
        return ResolveGenericType(typeName, typeof(Dictionary<,>));
    }

    public static Type ResolveGenericType(string typeName, params Type[] allowedDefinitions)
    {
        Type type = ResolveLoadedType(typeName);
        if (type.IsGenericType && allowedDefinitions.Contains(type.GetGenericTypeDefinition())) return type;

        throw new SerializationException($"Serialized generic type {type} is not allowed");
    }
}
