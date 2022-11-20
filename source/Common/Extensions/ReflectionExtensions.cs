using System;
using System.Collections.Generic;
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

        private readonly static BindingFlags AllInstanceFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        /// <summary>
        /// Gets all instance fields of a given type
        /// </summary>
        /// <param name="type">Type to get fields from</param>
        /// <param name="excluding">Optional: fields to exclude by name</param>
        /// <returns>Array of all instance fields</returns>
        public static FieldInfo[] GetAllInstanceFields(this Type type, IEnumerable<string> excluding = null)
        {
            if(excluding == null) return type.GetFields(AllInstanceFields);

            HashSet<string> excludes = new HashSet<string>(excluding);

            FieldInfo[] fields = type.GetFields(AllInstanceFields);

            return fields.Where(f => excludes.Contains(f.Name) == false && f.IsLiteral == false).ToArray();
        }

        /// <summary>
        /// Checks if a type is fully serializable
        /// </summary>
        /// <param name="type">Type to check serializability</param>
        /// <returns>True if fully serializable otherwise False</returns>
        public static bool IsFullySerializable(this Type type)
        {
            bool result = true;

            result &= type.IsSerializable;

            foreach(Type genericType in type.GetGenericArguments())
            {
                result &= genericType.IsFullySerializable();
                if (result == false) return result;
            }

            return result;
        }
    }
}
