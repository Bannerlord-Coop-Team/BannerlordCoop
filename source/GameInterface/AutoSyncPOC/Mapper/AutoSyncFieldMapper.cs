using GameInterface.AutoSync;
using HarmonyLib;
using System.Reflection;

namespace GameInterface.AutoSyncPOC.Mapper;

public interface IAutoSyncFieldMapper
{
    bool TryGetFieldMap(object instance, int fieldId, out AutoSyncFieldMap fieldMap);
    bool TryGetFieldMap(object instance, FieldInfo field, out AutoSyncFieldMap fieldMap);
}


public sealed class AutoSyncFieldMapper : IAutoSyncFieldMapper
{
    private readonly INetworkIdRegistry networkIdRegistry;
    private readonly IFieldRegistry fieldRegistry;

    public AutoSyncFieldMapper(INetworkIdRegistry networkIdRegistry, IFieldRegistry fieldRegistry)
    {
        this.networkIdRegistry = networkIdRegistry;
        this.fieldRegistry = fieldRegistry;
    }
    public bool TryGetFieldMap(object instance, int fieldId, out AutoSyncFieldMap fieldMap)
    {
        fieldMap = default;

        if (!fieldRegistry.TryGetField(fieldId, out var fieldInfo)) return false;

        return TryGetFieldMap(instance, fieldInfo, out fieldMap);
    }

    public bool TryGetFieldMap(object instance, FieldInfo field, out AutoSyncFieldMap fieldMap)
    {
        fieldMap = default;
        if (!networkIdRegistry.TryGetId(instance, out var networkId))
            return false;

        if (!fieldRegistry.TryGetId(field, out var fieldId))
            return false;


        fieldMap = new AutoSyncFieldMap(
            networkId,
            fieldId
        );

        return true;
    }
}
