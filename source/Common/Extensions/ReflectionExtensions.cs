using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Gets all types in an AppDomain
        /// </summary>
        /// <remarks>
        /// When running in tests and some other situations
        /// reflection throws a ReflectionTypeLoadException,
        /// Try statement is for mitigation.
        /// </remarks>
        /// <param name="domain">Domain to get types from</param>
        /// <returns>Enumarable of all domain types</returns>
        public static IEnumerable<Type> GetDomainTypes(this AppDomain domain)
        {
            List<Type> types = new List<Type>();

            Assembly[] assemblies = domain.GetAssemblies();
            foreach (Assembly _assembly in assemblies)
            {
                try
                {
                    foreach (Type type in _assembly.GetTypes())
                    {
                        types.Add(type);
                    }
                }
                catch (ReflectionTypeLoadException) { }
            }

            return types;
        }

        /// <summary>
        /// Gets all instance fields of a given type
        /// </summary>
        /// <param name="type">Type to get fields from</param>
        /// <param name="excluding">Optional: fields to exclude by name</param>
        /// <returns>Array of all instance fields</returns>
        public static FieldInfo[] GetAllInstanceFields(this Type type, IEnumerable<string> excluding = null)
        {
            if(excluding == null) return GetAllFieldsRecursive(type).ToArray();

            HashSet<string> excludes = new HashSet<string>(excluding);

            FieldInfo[] fields = GetAllFieldsRecursive(type).ToArray();

            return fields.Where(f => excludes.Contains(f.Name) == false && f.IsLiteral == false).ToArray();
        }

        private readonly static BindingFlags AllInstanceFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        private static IEnumerable<FieldInfo> GetAllFieldsRecursive(Type type, HashSet<Type> handledTypes = null)
        {
            if (handledTypes == null) handledTypes = new HashSet<Type>();

            if (handledTypes.Contains(type)) return new FieldInfo[0];

            handledTypes.Add(type);

            List<FieldInfo> fields = new List<FieldInfo>(type.GetFields(AllInstanceFields));

            if(type.BaseType != null)
            {
                fields.AddRange(GetAllFieldsRecursive(type.BaseType));
            }

            return fields;
        }

        /// <summary>
        /// Checks if a type is fully serializable
        /// </summary>
        /// <param name="type">Type to check serializability</param>
        /// <returns>True if fully serializable otherwise False</returns>
        public static bool IsFullySerializable(this Type type)
        {
            return IsFullySerializableRecursive(type, new HashSet<Type>());
        }

        private static bool IsFullySerializableRecursive(Type type, HashSet<Type> handledTypes = null)
        {
            bool result = true;

            if (handledTypes == null) handledTypes = new HashSet<Type>();

            if (handledTypes.Contains(type)) return result;

            result &= type.IsSerializable;

            foreach (Type genericType in type.GetGenericArguments())
            {
                result &= IsFullySerializableRecursive(genericType);
                if (result == false) return result;
            }

            if (type.IsArray) result &= IsFullySerializableRecursive(type.GetElementType());

            return result;
        }
    }
}
