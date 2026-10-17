using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace GameInterface.Serialization;

internal static class SerializedTypeResolver
{
    private const int MaxTokens = 64;
    private const string GenericCodes = "lhdkvrm";
    private static readonly Type[] GenericDefinitions =
    {
        typeof(List<>), typeof(HashSet<>), typeof(Dictionary<,>), typeof(KeyValuePair<,>),
        typeof(ValueTuple<,>), typeof(TaleWorlds.Library.MBReadOnlyList<>), typeof(TaleWorlds.Library.MBList<>),
    };
    public static string Encode(Type type)
    {
        var tokens = new List<string>();
        AddTokens(type, tokens);
        return string.Join("|", tokens);
    }

    public static Type ResolveType(string descriptor, params Type[] allowedDefinitions)
    {
        string[] tokens = descriptor?.Split(new[] { '|' }, MaxTokens + 1);
        if (tokens == null || tokens.Length == 0 || tokens.Length > MaxTokens)
            throw new SerializationException("Serialized type descriptor was invalid");

        int index = 0;
        Type type = ReadType(tokens, ref index);
        if (index != tokens.Length) throw new SerializationException("Serialized type descriptor contained trailing data");

        Type definition = type.IsArray ? typeof(Array) :
            type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (allowedDefinitions.Length > 0 && !allowedDefinitions.Contains(definition))
            throw new SerializationException($"Serialized type {type} is not allowed here");
        return type;
    }

    public static bool IsAllowedExactType(Type type)
    {
        if (type == null) return false;
        TypeCode code = Type.GetTypeCode(type);
        string assemblyName = type.Assembly.GetName().Name;
        return type == typeof(object) || type == typeof(Guid) || type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) || type == typeof(Tuple<uint, float>) ||
               (!type.IsEnum && code != TypeCode.Empty && code != TypeCode.Object && code != TypeCode.DBNull) ||
               type.Assembly == typeof(IBinaryPackage).Assembly || assemblyName == "Common" ||
               assemblyName.StartsWith("TaleWorlds.", StringComparison.Ordinal);
    }

    private static void AddTokens(Type type, List<string> tokens)
    {
        if (IsAllowedExactType(type))
        {
            tokens.Add("e");
            tokens.Add(type.Assembly.FullName);
            tokens.Add(type.FullName);
            return;
        }
        if (type?.IsArray == true && type.GetArrayRank() == 1)
        {
            tokens.Add("a");
            AddTokens(type.GetElementType(), tokens);
            return;
        }

        int definitionIndex = type?.IsGenericType == true
            ? Array.IndexOf(GenericDefinitions, type.GetGenericTypeDefinition()) : -1;
        if (definitionIndex < 0)
            throw new SerializationException($"Type {type} is not part of the binary package contract");

        tokens.Add(GenericCodes[definitionIndex].ToString());
        foreach (Type argument in type.GetGenericArguments()) AddTokens(argument, tokens);
    }

    private static Type ReadType(string[] tokens, ref int index)
    {
        if (index >= tokens.Length) throw new SerializationException("Serialized type descriptor was incomplete");

        string code = tokens[index++];
        if (code == "e") return ResolveExactType(tokens, ref index);
        if (code == "a") return ReadType(tokens, ref index).MakeArrayType();

        int definitionIndex = code.Length == 1 ? GenericCodes.IndexOf(code[0]) : -1;
        if (definitionIndex < 0) throw new SerializationException($"Serialized type code {code} is not allowed");

        Type definition = GenericDefinitions[definitionIndex];
        Type[] arguments = new Type[definition.GetGenericArguments().Length];
        for (int i = 0; i < arguments.Length; i++) arguments[i] = ReadType(tokens, ref index);
        return definition.MakeGenericType(arguments);
    }

    private static Type ResolveExactType(string[] tokens, ref int index)
    {
        if (index + 1 >= tokens.Length) throw new SerializationException("Serialized exact type was incomplete");

        string assemblyName = tokens[index++];
        string typeName = tokens[index++];
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.FullName == assemblyName);
        Type type = assembly?.GetType(typeName, false, false);
        if (!IsAllowedExactType(type))
            throw new SerializationException($"Serialized type {typeName} is not part of the binary package contract");
        return type;
    }
}
