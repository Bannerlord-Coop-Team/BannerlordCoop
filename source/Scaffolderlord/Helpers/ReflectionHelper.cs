using HarmonyLib;
using Scaffolderlord.Exceptions;
using Scaffolderlord.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.Helpers
{
    public class ReflectionHelper
    {
        public static ServiceTypeInfo GetServiceTypeInfo(string typeFullyQualifiedName, string[]? propertyNames = null, string[]? fieldNames = null, string[]? collectionNames = null)
        {
            propertyNames ??= Array.Empty<string>();
            fieldNames ??= Array.Empty<string>();
            collectionNames ??= Array.Empty<string>();

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
