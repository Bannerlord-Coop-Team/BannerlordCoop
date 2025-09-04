using GameInterface.DynamicSync.Builders;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync
{
    public class DynamicSyncRegistry
    {
        public readonly Dictionary<Type, DynamicSyncRegistryItem> Registrations = new Dictionary<Type, DynamicSyncRegistryItem>();

        public readonly Dictionary<Type, DynamicSyncRegistrySerializer> Serializers = new Dictionary<Type, DynamicSyncRegistrySerializer>();
        public readonly DynamicSyncRegistrySerializer DefaultSerializer = new DynamicSyncRegistrySerializer
        {
            Serialize = AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Serialize)),
            Deserialize = AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Deserialize))
        };

        public void AddField(FieldInfo field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));

            // TODO: verify interface support
            if (field.FieldType.IsInterface) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: Interfaces are currently not supported");

            if (!AddMember(field.DeclaringType, field)) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: {field.Name} has already been registered as a synced field");
        }

        public void AddProperty(PropertyInfo property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            // only prevent properties from being added if they are no collection like type
            if (property.CanWrite == false) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: {property.Name} does not have a set method");

            // TODO: verify interface support
            if (property.PropertyType.IsInterface) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: Interfaces are currently not supported");

            if (!AddMember(property.DeclaringType, property)) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: {property.Name} has already been registered as a synced property");
        }

        public bool AddTargetMethod(Type type, MethodInfo methodInfo)
        {

            if (!Registrations.ContainsKey(type))
            {
                Registrations.Add(type, new DynamicSyncRegistryItem());
            }

            if (Registrations[type].TargetMethods.Contains(methodInfo))
                return false;

            Registrations[type].TargetMethods.Add(methodInfo);

            return true;
        }

        /// <summary>
        /// Add CustomSerializers for DynamicSync
        /// </summary>
        /// <typeparam name="TargetType"></typeparam>
        /// <param name="serialize"></param>
        /// <param name="deserialize"></param>
        public void AddSerializer<TargetType>(Func<TargetType, byte[]> serialize, Func<byte[], TargetType, TargetType>deserialize)
        {
            DynamicSyncRegistrySerializer serializer = new DynamicSyncRegistrySerializer
            {
                Serialize = serialize.GetMethodInfo(),
                Deserialize = deserialize.GetMethodInfo()
            };
            Serializers.Add(typeof(TargetType), serializer);
        }

        private bool AddMember(Type type, MemberInfo memberInfo)
        {
            if (memberInfo is not FieldInfo && memberInfo is not PropertyInfo)
                return false;

            if (!Registrations.ContainsKey(type))
            {
                Registrations.Add(type, new DynamicSyncRegistryItem());
            }
            if(memberInfo is FieldInfo fieldInfo)
            { 
                if (Registrations[type].Fields.Contains(fieldInfo))
                    return false;

                Registrations[type].Fields.Add(fieldInfo);
            }
            else if (memberInfo is PropertyInfo propertyInfo)
            {
                if (Registrations[type].Properties.Contains(propertyInfo))
                    return false;

                Registrations[type].Properties.Add(propertyInfo);
            }
            else
                throw new NotSupportedException($"Unsupported MemberInfo Type: {memberInfo.MemberType}");

            return true;
        }
    }
}
