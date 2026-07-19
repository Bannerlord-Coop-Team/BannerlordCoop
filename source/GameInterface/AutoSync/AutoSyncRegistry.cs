using GameInterface.AutoSync.Builders;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public class AutoSyncRegistry
{
    public readonly Dictionary<Type, AutoSyncRegistryItem> Registrations = new Dictionary<Type, AutoSyncRegistryItem>();

    public readonly Dictionary<Type, AutoSyncRegistrySerializer> Serializers = new Dictionary<Type, AutoSyncRegistrySerializer>();
    public readonly AutoSyncRegistrySerializer DefaultSerializer = new AutoSyncRegistrySerializer
    {
        Serialize = AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Serialize)),
        Deserialize = AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Deserialize))
    };
    
    public readonly List<Action<object, object>> ReadonlySetters = new List<Action<object, object>>();

    public void AddField(FieldInfo field, bool debug = false, bool coalesce = false)
    {
        if (field == null) throw new ArgumentNullException(nameof(field));

        if (!AddMember(field.DeclaringType, field, debug, coalesce)) throw new ArgumentException($"{nameof(AutoSyncBuilder)} Field: {field.Name} has already been registered as a synced field");
    }

    public void AddProperty(PropertyInfo property, bool debug = false, bool coalesce = false)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));

        // only prevent properties from being added if they are no collection like type
        if (property.CanWrite == false) throw new ArgumentException($"{nameof(AutoSyncBuilder)} Property: {property.Name} does not have a set method");

        if (!AddMember(property.DeclaringType, property, debug, coalesce)) throw new ArgumentException($"{nameof(AutoSyncBuilder)} Property: {property.Name} has already been registered as a synced property");
    }

    public bool AddTargetMethod(Type type, MethodInfo methodInfo, string patchCategory = null)
    {

        if (!Registrations.ContainsKey(type))
        {
            Registrations.Add(type, new AutoSyncRegistryItem());
        }

        if (Registrations[type].TargetMethods.Contains(methodInfo))
            return false;

        Registrations[type].AddTargetMethod(methodInfo, patchCategory);

        return true;
    }

    /// <summary>
    /// Add CustomSerializers for AutoSync
    /// </summary>
    /// <typeparam name="TargetType"></typeparam>
    /// <param name="serialize"></param>
    /// <param name="deserialize"></param>
    public void AddSerializer<TargetType>(Func<TargetType, byte[]> serialize, Func<byte[], TargetType, TargetType> deserialize)
    {
        AutoSyncRegistrySerializer serializer = new AutoSyncRegistrySerializer
        {
            Serialize = serialize.GetMethodInfo(),
            Deserialize = deserialize.GetMethodInfo()
        };
        Serializers.Add(typeof(TargetType), serializer);
    }

    public int AddReadOnlySetter(Action<object, object> accessor)
    {
        ReadonlySetters.Add(accessor);

        return ReadonlySetters.Count - 1;
    }

    private bool AddMember(Type type, MemberInfo memberInfo, bool debug, bool coalesce)
    {
        if (memberInfo is not FieldInfo && memberInfo is not PropertyInfo)
            return false;

        if (!Registrations.ContainsKey(type))
        {
            Registrations.Add(type, new AutoSyncRegistryItem());
        }
        if(memberInfo is FieldInfo fieldInfo)
        {
            if (Registrations[type].Contains(fieldInfo))
                return false;

            Registrations[type].AddField(fieldInfo, debug, coalesce);
        }
        else if (memberInfo is PropertyInfo propertyInfo)
        {
            if (Registrations[type].Contains(propertyInfo))
                return false;

            Registrations[type].AddProperty(propertyInfo, debug, coalesce);
        }
        else
            throw new NotSupportedException($"Unsupported MemberInfo Type: {memberInfo.MemberType}");

        return true;
    }
}
