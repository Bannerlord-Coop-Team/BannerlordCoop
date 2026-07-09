using GameInterface.AutoSync.Templates;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public static class AutoSyncUtils
{
    public static string GetPrefix(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        return TemplateParser.Parse("Patches.PropertySetPrefixTemplate",
            new
            {
                MemberDeclaringType = GetSimpleTypeName(propertyInfo.DeclaringType),
                MemberDeclaringTypeName = GetSimpleTypeName(propertyInfo.DeclaringType).Replace(".", "_"),
                MemberName = propertyInfo.Name,
                MemberType = GetSimpleTypeName(propertyInfo.PropertyType),
                Debug = propertyItem.Debug
            });
    }

    public static string GetLocalSetMessage(MemberInfo memberInfo)
    {
        Type memberType;
        string memberName;
        if (memberInfo is PropertyInfo property)
        {
            memberType = property.PropertyType;
            memberName = property.Name;
        }
        else if (memberInfo is FieldInfo field)
        {
            memberType = field.FieldType;
            memberName = field.Name;
        }
        else
            throw new NotSupportedException($"MemberInfoType {memberInfo.MemberType} is not supported by AutoSync");

        var libraries = new List<string>
        {
            memberInfo.DeclaringType.Namespace,
            GetNamespace(memberType)
        };

        foreach (var elementType in GetElementTypes(memberType))
            libraries.Add(GetNamespace(elementType));

        return TemplateParser.Parse("Messages.LocalSetMessageTemplate",
            new
            {
                MemberDeclaringType = GetSimpleTypeName(memberInfo.DeclaringType),
                MemberDeclaringTypeName = GetSimpleTypeName(memberInfo.DeclaringType).Replace(".", "_"),
                MemberName = memberName,
                MemberType = GetMemberTypeName(memberType),
                Libraries = libraries
            });
    }

    public static string GetMemberTypeName(Type type)
    {
        if (type.IsArray)
            return $"{GetSimpleTypeName(type.GetElementType())}[]";
        else if (type.IsGenericType)
        {
            if (type.Name.ToLower().Contains("dictionary"))
            {
                var genericArguments = type.GetGenericArguments();
                return $"Dictionary<{GetSimpleTypeName(genericArguments[0])}, {GetSimpleTypeName(genericArguments[1])}>";
            }

            var arg = GetSimpleTypeName(type.GetGenericArguments()[0]);
            if (type.Name.ToLower().Contains("mblist")) return $"MBList<{arg}>";
            if (type.Name.ToLower().Contains("list")) return $"List<{arg}>";
            if (type.Name.ToLower().Contains("queue")) return $"Queue<{arg}>";
            throw new NotSupportedException($"CollectionType {type.Name} is not supported by AutoSync");
        }
        else
            return GetSimpleTypeName(type);
    }

    public static string GetSimpleTypeName(Type type)
    {
        if (type.IsNested)
            return $"{GetSimpleTypeName(type.DeclaringType)}.{type.Name}";
        return type.Name;
    }

    public static string GetNamespace(Type type)
    {
        return type.Namespace;
    }

    public static IEnumerable<string> GetLibraries(MemberInfo memberInfo)
    {
        Type memberType;
        if (memberInfo is PropertyInfo propertyInfo)
        {
            memberType = propertyInfo.PropertyType;
            yield return GetNamespace(propertyInfo.DeclaringType);
        }
        else if (memberInfo is FieldInfo fieldInfo)
        {
            memberType = fieldInfo.FieldType;
            yield return GetNamespace(fieldInfo.DeclaringType);
        }
        else
        {
            throw new NotSupportedException($"Unsupported MemberInfo of type {memberInfo.MemberType} for GetLibraries");
        }

        foreach (var elementType in GetElementTypes(memberType))
        {
            yield return GetNamespace(elementType);
        }
        yield return GetNamespace(memberType);
    }

    private static IEnumerable<Type> GetElementTypes(Type type)
    {
        // Multi-argument generics (e.g. Dictionary<TKey, TValue>) need every argument's namespace
        if (type.IsArray)
            yield return type.GetElementType();
        else if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                yield return argument;
        }
    }
}