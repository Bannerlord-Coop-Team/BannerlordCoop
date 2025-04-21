using GameInterface.DynamicSync.Builders;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.DynamicSync
{
    public class DynamicSyncRegistry
    {
        public readonly Dictionary<Type, DynamicSyncRegistryItem> Registrations = new Dictionary<Type, DynamicSyncRegistryItem>();

        // TODO:Fix
        public Assembly Assembly;

        public void AddField(FieldInfo field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));

            // TODO: Add back collection support
            if (field.FieldType.IsGenericType || field.FieldType.IsArray) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: Collection types are currently not supported");

            // TODO: verify interface support
            if (field.FieldType.IsInterface) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: Interfaces are currently not supported");

            if (!AddMember(field.DeclaringType, field)) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: {field.Name} has already been registered as a synced field");
        }

        public void AddProperty(PropertyInfo property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            // only prevent properties from being added if they are no collection like type
            if (property.CanWrite == false) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: {property.Name} does not have a set method");

            // TODO: Add back collection support
            if (property.PropertyType.IsGenericType || property.PropertyType.IsArray) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: Collection types are currently not supported");

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

        public bool TryGetIntercept(FieldInfo fieldInfo, out MethodInfo intercept, DynamicMessageAction dynamicMessageAction = DynamicMessageAction.Set)
        {
            intercept = null;
            if (dynamicMessageAction == DynamicMessageAction.None)
                throw new InvalidOperationException("Not allowed Intercept Access");
            
            if (!Registrations.TryGetValue(fieldInfo.DeclaringType, out var registryItem))
                return false;
            
            var member = registryItem.Fields.FirstOrDefault(m => m == fieldInfo);
            if (member == null)
                return false;

            var dynamicPatch = Assembly.GetType($"DynamicSync.{fieldInfo.DeclaringType.Name}DynamicPatches");
            if (dynamicPatch == null)
                return false;

            var genericPatch = dynamicPatch.BaseType;

            if(dynamicMessageAction == DynamicMessageAction.Set)
            {
                var messageType = Assembly.GetType($"DynamicSync.{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetMessage");
                var fieldIntercept = genericPatch.GetMethod("FieldIntercept").MakeGenericMethod(fieldInfo.FieldType, messageType);
                intercept = fieldIntercept;
            }

            // TODO: Add Intercept for other actions like adding elements to collection

            return true;
        }
    }
}
