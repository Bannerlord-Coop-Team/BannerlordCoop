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

    /// <summary>
    /// Rebinds the generated AutoSync apply-handlers (and re-registers their serializable types) onto the
    /// current container without re-applying the Harmony patches. The patches persist for the whole process,
    /// but the handlers and type mapper are container-scoped and are torn down when a client disconnects, so
    /// on reconnect they must be rebound here - otherwise nothing subscribes and every synced update is
    /// dropped. Re-running PatchAllUncategorized on the static generated assembly would double-patch it.
    /// </summary>
    public void RebindHandlers()
    {
        if (!AutoSyncConfiguration.Enabled) return;
        if (Assembly == null) return;

        BindHandlers(Assembly);
    }
}
