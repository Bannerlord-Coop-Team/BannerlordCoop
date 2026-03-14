using Common.Network;
using GameInterface.AutoSync;
using GameInterface.AutoSyncPOC.Mapper;
using GameInterface.AutoSyncPOC.Messages;
using Serilog;
using System;

namespace GameInterface.AutoSyncPOC;

public interface IFieldProcessor
{
    void SendReference<TInstance, TField>(TInstance instance, int fieldId, TField value);
    void SendValue<TInstance, TField>(TInstance instance, int fieldId, TField value);
    void SetValue<TInstance, TField>(TInstance instance, int fieldId, TField value);
}

internal class FieldProcessor : IFieldProcessor
{
    private readonly ILogger logger;
    private readonly IFieldRegistry fieldRegistry;
    private readonly IAutoSyncFieldMapper fieldMapper;
    private readonly INetwork network;
    private readonly INetworkIdRegistry networkIdRegistry;

    public FieldProcessor(
        ILogger logger,
        IFieldRegistry fieldRegistry,
        IAutoSyncFieldMapper fieldMapper,
        INetwork network,
        INetworkIdRegistry networkIdRegistry)
    {
        this.logger = logger;
        this.fieldRegistry = fieldRegistry;
        this.fieldMapper = fieldMapper;
        this.network = network;
        this.networkIdRegistry = networkIdRegistry;
    }

    public void SendReference<TInstance, TField>(TInstance instance, int fieldId, TField value)
    {
        if (!fieldMapper.TryGetFieldMap(instance, fieldId, out var fieldMap))
        {
            logger.Error("Failed to get typemap for fieldId {fieldId}", fieldId);
            return;
        }

        if (value is null)
        {
            network.SendAll(new SetFieldNull(fieldMap));
            return;
        }

        if (!networkIdRegistry.TryGetId(value, out var networkId))
        {
            logger.Error(
                "Failed to get network id for {valueType}\n" +
                "Callstack: {callstack}",
                typeof(TField),
                Environment.StackTrace);
        }

        var bytes = RawSerializer.Serialize(networkId);

        var message = new SetFieldCommand(fieldMap, bytes);

        network.SendAll(message);
    }

    public void SendValue<TInstance, TField>(TInstance instance, int fieldId, TField value)
    {
        if (!fieldMapper.TryGetFieldMap(instance, fieldId, out var fieldMap))
        {
            logger.Error("Failed to get typemap for fieldId {fieldId}", fieldId);
            return;
        }

        if (value is null)
        {
            network.SendAll(new SetFieldNull(fieldMap));
            return;
        }

        var bytes = RawSerializer.Serialize(value);

        var message = new SetFieldCommand(fieldMap, bytes);

        network.SendAll(message);
    }

    public void SetValue<TInstance, TField>(TInstance instance, int fieldId, TField value)
    {
        if (!fieldRegistry.TryGetSetter<TInstance, TField>(fieldId, out var setter))
        {
            logger.Error("Failed to get field setter");
            return;
        }

        setter(instance, value);
    }
}
