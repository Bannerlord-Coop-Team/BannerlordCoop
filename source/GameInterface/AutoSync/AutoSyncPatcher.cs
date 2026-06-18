using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.AutoSync.Builders;
using HarmonyLib;
using ProtoBuf;
using System.Linq;
using System.Reflection;

namespace GameInterface.AutoSync;

public class AutoSyncPatcher
{
    public static Assembly Assembly;

    private readonly Harmony harmony;
    private readonly AutoSyncBuilder autoSyncBuilder;
    private readonly AutoSyncHandler dynamicHandler;
    private readonly ISerializableTypeMapper serializableTypeMapper;

    public AutoSyncPatcher(Harmony harmony, AutoSyncBuilder autoSyncBuilder, AutoSyncHandler dynamicHandler, ISerializableTypeMapper serializableTypeMapper)
    {
        this.harmony = harmony;
        this.autoSyncBuilder = autoSyncBuilder;
        this.dynamicHandler = dynamicHandler;
        this.serializableTypeMapper = serializableTypeMapper;
    }

    /// <summary>
    /// Only required for testing to be able to rebind the handlers on client side
    /// </summary>
    /// <param name="assembly"></param>
    public void BindHandlers(Assembly assembly)
    {
        serializableTypeMapper.AddTypes(assembly.GetTypes()
        .Where(type => {
            try
            {
                return type.IsDefined(typeof(ProtoContractAttribute), inherit: false);
            }
            // Some types have malformed attributes?
            catch (CustomAttributeFormatException)
            {
                return false;
            }
        }));

        var handlers = assembly.GetTypes()
            .Where(type => type.IsAssignableTo<IHandler>());

        foreach (var handler in handlers)
        {
            dynamicHandler.RegisterHandler(handler);
        }
    }

    public void PatchAll()
    {
        if (!AutoSyncConfiguration.Enabled) return;

        if (Assembly == null)
            Assembly = autoSyncBuilder.Build();

        harmony.PatchAllUncategorized(Assembly);
        BindHandlers(Assembly);
    }
}
