using GameInterface.DynamicSync.Templates;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders;

public static class DynamicSyncUtils
{
    public static string GetPrefix(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        return TemplateParser.Parse("Patches.PropertySetPrefixTemplate",
            new
            {
                MemberDeclaringType = GetSimpleTypeName(propertyInfo.DeclaringType),
                MemberDeclaringTypeName = propertyInfo.DeclaringType.Name,
                MemberName = propertyInfo.Name,
                MemberType = GetSimpleTypeName(propertyInfo.PropertyType),
                MemberTypeName = propertyInfo.PropertyType.Name,
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
            throw new NotSupportedException($"MemberInfoType {memberInfo.MemberType} is not supported by DynamicSync");

        var libraries = new List<string>
        {
            memberInfo.DeclaringType.Namespace,
            GetNamespace(memberType)
        };

        if (memberType.IsArray || memberType.IsGenericType)
            libraries.Add(GetNamespace(GetElementType(memberType)));

        return TemplateParser.Parse("Messages.LocalSetMessageTemplate",
            new
            {
                MemberDeclaringType = GetSimpleTypeName(memberInfo.DeclaringType),
                MemberDeclaringTypeName = memberInfo.DeclaringType.Name,
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
            var arg = GetSimpleTypeName(type.GetGenericArguments()[0]);
            if (type.Name.ToLower().Contains("mblist")) return $"MBList<{arg}>";
            if (type.Name.ToLower().Contains("list")) return $"List<{arg}>";
            if (type.Name.ToLower().Contains("queue")) return $"Queue<{arg}>";
            throw new NotSupportedException($"CollectionType {type.Name} is not supported by DynamicSync");
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

        if (memberType.IsArray || memberType.IsGenericType)
        {
            yield return GetNamespace(GetElementType(memberType));
        }
        yield return GetNamespace(memberType);
    }

    private static Type GetElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();
        else
            return type.GetGenericArguments()[0];
    }
}