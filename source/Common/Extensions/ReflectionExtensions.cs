using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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
        public static IEnumerable<Type> GetDomainTypes(this AppDomain domain, string namespacePrefix = null)
        {
            namespacePrefix = namespacePrefix == null ? string.Empty : namespacePrefix;

            List<Type> types = new List<Type>();

            Assembly[] assemblies = domain.GetAssemblies();
            foreach (Assembly _assembly in assemblies)
            {
                try
                {
                    foreach (Type type in _assembly.GetTypes())
                    {
                        if (type.Namespace == null) continue;
                        if (type.Namespace.StartsWith(namespacePrefix) == false) continue;

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
            if (excluding == null) return GetAllFieldsRecursive(type).ToArray();

            HashSet<string> excludes = new HashSet<string>(excluding);

            List<FieldInfo> fields = new List<FieldInfo>();

            foreach (var field in GetAllFieldsRecursive(type).ToArray())
            {
                if (excludes.Contains(field.Name))
                {
                    excludes.Remove(field.Name);
                }
                else if (field.IsLiteral == false)
                {
                    fields.Add(field);
                }
            }

            if (excludes.Count() > 0)
            {
                throw new ArgumentException(
                $"Some excluding values where not present in retrieved fields. " +
                $"These values where not found {excludes.ValuesToString()}");
            }

            return fields.ToArray();
        }

        private readonly static BindingFlags AllInstanceFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        private static IEnumerable<FieldInfo> GetAllFieldsRecursive(Type type, HashSet<Type> handledTypes = null)
        {
            if (handledTypes == null) handledTypes = new HashSet<Type>();

            if (handledTypes.Contains(type)) return new FieldInfo[0];

            handledTypes.Add(type);

            List<FieldInfo> fields = new List<FieldInfo>(type.GetFields(AllInstanceFields));

            if (type.BaseType != null)
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

        /// <summary>
        /// Castable GetValue Extension
        /// </summary>
        /// <typeparam name="T">Value to cast to</typeparam>
        /// <param name="field">Extended type</param>
        /// <param name="obj">Object to get value from</param>
        /// <returns></returns>
        public static T GetValue<T>(this FieldInfo field, object obj)
        {
            return (T)field.GetValue(obj);
        }

        /// <summary>
        /// Castable GetValue Extension
        /// </summary>
        /// <typeparam name="T">Value to cast to</typeparam>
        /// <param name="property">Extended type</param>
        /// <param name="obj">Object to get value from</param>
        /// <returns></returns>
        public static T GetValue<T>(this PropertyInfo property, object obj)
        {
            return (T)property.GetValue(obj);
        }

        /// <summary>
        /// Creates a new delegate with error checking
        /// </summary>
        /// <param name="type">Delegate type</param>
        /// <param name="obj">Instance used to call method</param>
        /// <param name="method">Method to assign delegate to</param>
        /// <param name="delegate">Newly created delegate</param>
        /// <returns>Success status</returns>
        public static bool TryCreateDelegate(this Type type, object obj, MethodInfo method, out Delegate @delegate)
        {
            @delegate = null;

            if (obj == null) return false;

            @delegate = Delegate.CreateDelegate(type, obj, method);

            return true;
        }

        private static int seed = 9876;
        private static Random random = new Random(seed);
        private static Dictionary<Type, Func<object>> RandomTypeMap = new Dictionary<Type, Func<object>>
        {
            { typeof(int), () => random.Next() },
            { typeof(float), () => (float)random.NextDouble() },
            { typeof(double), () => random.NextDouble() },
            { typeof(string), () => random.NextDouble().ToString() },
            { typeof(bool), () => random.Next(2) > 0 ? true : false },
        };

        /// <summary>
        /// Returns random value of <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">Type of random value</typeparam>
        /// <returns>Random value of <typeparamref name="T"/> or default of <typeparamref name="T"/> if failed</returns>
        public static T Random<T>()
        {
            if (TryChooseRandomType(typeof(T), out var result))
            {
                return (T)result;
            }
            return default(T);
        }

        /// <summary>
        /// Sets the field of an object to a random type if it is handled
        /// </summary>
        /// <param name="field">Field to assign</param>
        /// <param name="obj">Object to assign</param>
        public static void SetRandom(this FieldInfo field, object obj)
        {
            if (TryChooseRandomType(field.FieldType, out object newObj))
            {
                field.SetValue(obj, newObj);
            }
        }

        /// <summary>
        /// Sets the property of an object to a random type if it is handled
        /// </summary>
        /// <param name="property">Property to assign</param>
        /// <param name="obj">Object to assign</param>
        public static void SetRandom(this PropertyInfo property, object obj)
        {
            if (property.SetMethod == null) return;

            if (TryChooseRandomType(property.PropertyType, out object newObj))
            {
                property.SetValue(obj, newObj);
            }
        }

        private static bool TryChooseRandomType(Type type, out object obj)
        {
            if (RandomTypeMap.TryGetValue(type, out Func<object> randFn))
            {
                obj = randFn();
                return true;
            }

            if (type.IsEnum)
            {
                obj = ChooseRandomEnum(type);
                return true;
            }

            obj = null;
            return false;
        }

        private static object ChooseRandomEnum(Type type)
        {
            Array values = Enum.GetValues(type);
            return values.GetValue(random.Next(values.Length));
        }
    }
}
