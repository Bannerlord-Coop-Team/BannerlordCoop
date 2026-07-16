using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace GameInterface.Serialization;

internal static class SerializedTypeResolver
{
    public static Type ResolveLoadedType(string assemblyQualifiedName)
    {
        if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
            throw new SerializationException("Serialized type name was empty");

        Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        Type resolved = Type.GetType(
            assemblyQualifiedName,
            assemblyName => ResolveLoadedAssembly(loadedAssemblies, assemblyName),
            (assembly, typeName, ignoreCase) => ResolveType(loadedAssemblies, assembly, typeName, ignoreCase),
            throwOnError: false,
            ignoreCase: false);

        if (resolved == null)
            throw new SerializationException($"Serialized type {assemblyQualifiedName} is not already loaded");

        return resolved;
    }

    public static Type ResolveEnumerableType(string assemblyQualifiedName)
    {
        Type type = ResolveLoadedType(assemblyQualifiedName);
        if (type.IsArray)
        {
            if (type.GetArrayRank() != 1)
                throw new SerializationException($"Only one-dimensional arrays are supported, not {type}");
            return type;
        }

        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(HashSet<>)) return type;
        }

        throw new SerializationException($"Serialized enumerable type {type} is not allowed");
    }

    public static Type ResolveDictionaryType(string assemblyQualifiedName)
    {
        Type type = ResolveLoadedType(assemblyQualifiedName);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) return type;

        throw new SerializationException($"Serialized dictionary type {type} is not allowed");
    }

    public static Type ResolveGenericType(string assemblyQualifiedName, params Type[] allowedDefinitions)
    {
        Type type = ResolveLoadedType(assemblyQualifiedName);
        if (type.IsGenericType && allowedDefinitions.Contains(type.GetGenericTypeDefinition())) return type;

        throw new SerializationException($"Serialized generic type {type} is not allowed");
    }

    private static Assembly ResolveLoadedAssembly(IEnumerable<Assembly> loadedAssemblies, AssemblyName requested)
    {
        return loadedAssemblies.FirstOrDefault(assembly =>
            string.Equals(assembly.FullName, requested.FullName, StringComparison.Ordinal));
    }

    private static Type ResolveType(
        IEnumerable<Assembly> loadedAssemblies,
        Assembly assembly,
        string typeName,
        bool ignoreCase)
    {
        if (assembly != null) return assembly.GetType(typeName, throwOnError: false, ignoreCase: ignoreCase);

        foreach (Assembly loadedAssembly in loadedAssemblies)
        {
            Type type = loadedAssembly.GetType(typeName, throwOnError: false, ignoreCase: ignoreCase);
            if (type != null) return type;
        }

        return null;
    }
}
