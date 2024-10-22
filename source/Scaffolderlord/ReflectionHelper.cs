using HarmonyLib;
using Scaffolderlord.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord
{
    public record ServiceTypeInfo
    {
        public ServiceTypeInfo(Type type)
        {
            this.Type = type;
        }

        public Type Type { get; set; }
        public List<PropertyInfo> Properties { get; set; } = new();
        public List<FieldInfo> Fields { get; set; } = new();
        public List<MemberInfo> Collections { get; set; } = new();
    }

    public class ReflectionHelper
    {
        public static ServiceTypeInfo GetServiceTypeInfo(string typeFullyQualifiedName, string[] propertyNames, string[] fieldNames, string[] collectionNames = null)
        {
            Type type = Type.GetType(typeFullyQualifiedName) ?? throw new TypeNotFoundException(typeFullyQualifiedName);

            var serviceTypeInfo = new ServiceTypeInfo(type);

            // gets propInfos
            foreach (var propertyName in propertyNames)
            {
                var propInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    ?? throw new PropertyNotFoundException(propertyName, type?.FullName);

                _ = propInfo.GetSetMethod(true) ?? throw new PropertyWithNoSetterException(propertyName, type?.FullName);

                serviceTypeInfo.Properties.Add(propInfo);
            }

            // gets fieldInfos
            foreach (var fieldName in fieldNames)
            {
                var fieldInfo = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    ?? throw new FieldNotFoundException(fieldName, type.FullName);

                serviceTypeInfo.Fields.Add(fieldInfo);
            }

            // gets collections...

            return serviceTypeInfo;
        }
    }
}
