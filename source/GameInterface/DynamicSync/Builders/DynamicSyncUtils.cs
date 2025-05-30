using GameInterface.DynamicSync.Templates;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders
{
    public static class DynamicSyncUtils
    {
        public static string GetSetTranspiler(FieldInfo fieldInfo)
        {
            return TemplateParser.Parse("Patches.FieldSetTranspilerTemplate",
            new
            {
                    MemberDeclaringType = fieldInfo.DeclaringType.Name,
                    MemberName = fieldInfo.Name,
                    MemberType = GetMemberTypeName(fieldInfo.FieldType)
            });
        }
        public static string GetPrefix(PropertyInfo propertyInfo)
        {
            return TemplateParser.Parse("Patches.PropertySetPrefixTemplate",
                new
                {
                    MemberDeclaringType = propertyInfo.DeclaringType.Name,
                    MemberName = propertyInfo.Name,
                    MemberType = propertyInfo.PropertyType.Name
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

            if(memberType.IsArray || memberType.IsGenericType)
                libraries.Add(GetNamespace(GetElementType(memberType)));

            return TemplateParser.Parse("Messages.LocalSetMessageTemplate",
                new
                {
                    MemberDeclaringType = memberInfo.DeclaringType.Name,
                    MemberName = memberName,
                    MemberType = GetMemberTypeName(memberType),
                    Libraries = libraries
                });
        }

        public static string GetMemberTypeName(Type type)
        {
            if (type.IsArray)
            {
                return $"{type.GetElementType().Name}[]";
            }
            else if (type.IsGenericType)
            {
                if (type.Name.ToLower().Contains("mblist"))
                    return $"MBList<{type.GetGenericArguments()[0].Name}>";
                else if (type.Name.ToLower().Contains("list"))
                    return $"List<{type.GetGenericArguments()[0].Name}>";
                else if (type.Name.ToLower().Contains("queue"))
                    return $"Queue<{type.GetGenericArguments()[0].Name}>";
                else
                    throw new NotSupportedException($"CollectionType {type.Name} is not supported by DynamicSync");
            }
            else
                return type.Name;
        }

        private static Type GetElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();
            else
                return type.GetGenericArguments()[0];
        }

        public static string GetNamespace(Type type)
        {
            string result = null;
            if(type.DeclaringType != null)
            {
                result = $".{GetDeclaringTypeName(type.DeclaringType)}";
            }

            return $"{(result != null ? "static " : "") }{type.Namespace}{result}";
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

        private static string GetDeclaringTypeName(Type type)
        {
            if(type.DeclaringType != null)
            {
                return $"{GetDeclaringTypeName(type.DeclaringType)}.{type.Name}";
            }
            return type.Name;
        }
    }
}
