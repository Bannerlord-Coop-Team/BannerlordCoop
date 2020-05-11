using System;
using System.Reflection;

namespace Coop.Reflection
{
    public static class Extensions
    {
        public static Type GetUnderlyingType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Event:
                    return ((EventInfo) member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo) member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo) member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo) member).PropertyType;
                default:
                    throw new ArgumentException(
                        "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo");
            }
        }

        public static MethodInfo[] GetDeclaredMethods(this Type type)
        {
            return type.GetMethods(
                BindingFlags.DeclaredOnly |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Public);
        }
    }
}
