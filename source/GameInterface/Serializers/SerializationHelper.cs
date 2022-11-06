using Coop.Mod.Serializers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serializers
{
    internal class SerializationHelper
    {
        
        /// <summary>
        /// Get all fields from type
        /// </summary>
        /// <returns>FieldInfo[]</returns>
        public static FieldInfo[] GetFields(Type type)
        {
            List<FieldInfo> fields = new List<FieldInfo>(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            if (type.BaseType != null)
            {
                fields.AddRange(GetFields(type.BaseType));
            }
            return fields.ToArray();
        }

        public static bool IsTypeSerializable(Type type)
        {
            return IsSerializableRecursive(type);
        }

        /// <summary>
        /// Checks if type is serializable and any template types
        /// </summary>
        /// <param name="type">Type from collection</param>
        /// <returns>If collection is completely serializable</returns>
        private static bool IsSerializableRecursive(Type type)
        {
            bool result = true;

            List<Type> templateTypes = new List<Type>(type.GetGenericArguments());

            result &= IsSerializable(type);

            foreach (Type elementType in templateTypes)
            {
                result &= IsSerializableRecursive(elementType);
            }
            return result;
        }

        private static bool IsSerializable(Type type)
        {
            return !SerializerConfig.MarkAsNonSerializable.Contains(type) && type.IsSerializable;
        }
    }
}
