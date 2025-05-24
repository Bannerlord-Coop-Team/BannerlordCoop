using GameInterface.DynamicSync.Builders;
using GameInterface.Services.ObjectManager;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync;

public class DynamicSyncRegistry
{
    public readonly Dictionary<Type, DynamicSyncRegistryItem> Registrations = new Dictionary<Type, DynamicSyncRegistryItem>();

    public void AddField(FieldInfo field)
    {
        if (field == null) throw new ArgumentNullException(nameof(field));

        // TODO: Add back collection support
        if (field.FieldType.IsGenericType || field.FieldType.IsArray) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: Collection types are currently not supported");

        // TODO: verify interface support
        if (field.FieldType.IsInterface) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: Interfaces are currently not supported");

        var declaringType = field.DeclaringType;

        if (Registrations.TryGetValue(declaringType, out var syncRegistry) == false)
        {
            syncRegistry = new DynamicSyncRegistryItem();
            Registrations.Add(declaringType, syncRegistry);
        }

        if (Registrations[declaringType].Fields.Contains(field))
            throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: {field.Name} has already been registered as a synced field");

        Registrations[declaringType].Fields.Add(field);
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

        var declaringType = property.DeclaringType;

        if (Registrations.TryGetValue(declaringType, out var syncRegistry) == false)
        {
            syncRegistry = new DynamicSyncRegistryItem();
            Registrations.Add(declaringType, syncRegistry);
        }

        if (Registrations[declaringType].Properties.Contains(property))
            throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: {property.Name} has already been registered as a synced property");

        Registrations[declaringType].Properties.Add(property);
    }

    public bool AddTargetMethod(Type type, MethodInfo methodInfo)
    {
        var declaringType = methodInfo.DeclaringType;

        if (!Registrations.ContainsKey(methodInfo.DeclaringType))
        {
            Registrations.Add(declaringType, new DynamicSyncRegistryItem());
        }

        if (Registrations[declaringType].TargetMethods.Contains(methodInfo))
            return false;

        Registrations[declaringType].TargetMethods.Add(methodInfo);

        return true;
    }
}
