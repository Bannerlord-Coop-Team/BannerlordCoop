using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.DynamicSync.Builders;
using HarmonyLib;
using ProtoBuf;
using System.Linq;
using System.Reflection;

namespace GameInterface.DynamicSync;

public class DynamicSyncPatcher
{
    public static Assembly Assembly;

    private readonly Harmony harmony;
    private readonly DynamicSyncBuilder dynamicSyncBuilder;
    private readonly DynamicHandler dynamicHandler;
    private readonly ISerializableTypeMapper serializableTypeMapper;

    public DynamicSyncPatcher(Harmony harmony, DynamicSyncBuilder dynamicSyncBuilder, DynamicHandler dynamicHandler, ISerializableTypeMapper serializableTypeMapper)
    {
        this.harmony = harmony;
        this.dynamicSyncBuilder = dynamicSyncBuilder;
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
        if (DynamicSyncConfiguration.Enabled)
        { 
            if (Assembly == null)
                Assembly = dynamicSyncBuilder.Build();

            harmony.PatchAllUncategorized(Assembly);
            BindHandlers(Assembly);
        }
        else
        {
            BindHandlers(Assembly.GetAssembly(typeof(DynamicSyncPatcher)));
        }
    }
}
