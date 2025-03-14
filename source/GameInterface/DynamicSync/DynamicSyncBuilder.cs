using GameInterface.DynamicSync;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;
public interface IDynamicSyncBuilder : IDisposable
{
    /// <summary>
    /// Add a field to automatically sync across the network
    /// </summary>
    /// <param name="field">Field to auto sync</param>
    void AddField(FieldInfo field);

    /// <summary>
    /// Add a property to automatically sync across the network
    /// </summary>
    /// <param name="property">Property to auto sync</param>
    void AddProperty(PropertyInfo property);

    /// <summary>
    /// Add further TargetMethods that access the sync properties/fields
    /// </summary>
    /// <param name="type">Type of the TargetClass the properties/fields belong to</param>
    /// <param name="methodInfo">MethodInfo of method outside TargetClass</param>
    void AddTargetMethod(Type type, MethodInfo methodInfo);
}
internal class DynamicSyncBuilder : IDynamicSyncBuilder
{
    private readonly HashSet<FieldInfo> fields = new HashSet<FieldInfo>();
    private readonly HashSet<PropertyInfo> properties = new HashSet<PropertyInfo>();
    private readonly DynamicSyncRegistry dynamicSyncRegistry;

    public DynamicSyncBuilder(DynamicSyncRegistry dynamicSyncRegistry)
    {
        this.dynamicSyncRegistry = dynamicSyncRegistry;
    }

    public void AddField(FieldInfo field)
    {
        if (field == null) throw new ArgumentNullException(nameof(field));

        if (!dynamicSyncRegistry.Add(field.DeclaringType, field)) throw new ArgumentException($"{field.Name} has already been registered as a synced field");
    }

    public void AddProperty(PropertyInfo property)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (property.CanWrite == false) throw new ArgumentException($"{property.Name} does not have a set method");

        if (!dynamicSyncRegistry.Add(property.DeclaringType, property)) throw new ArgumentException($"{property.Name} has already been registered as a synced property");
    }

    public void AddTargetMethod(Type type, MethodInfo methodInfo)
    {
        dynamicSyncRegistry.AddTargetMethod(type, methodInfo);
    }

    public void Dispose()
    {
    }
}
