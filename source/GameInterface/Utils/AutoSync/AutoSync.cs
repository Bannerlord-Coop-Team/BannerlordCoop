using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.AutoSync.Dynamic;
using GameInterface.Utils.AutoSync.Template;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Map;

namespace GameInterface.Utils.AutoSync;

internal interface IAutoSync : IDisposable
{
    public void SyncProperty<T>(PropertyInfo property, Func<T, string> stringIdGetter) where T : class;
}
internal class AutoSync : IAutoSync
{
    private readonly Harmony harmony;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ModuleBuilder moduleBuilder;

    private readonly List<IDisposable> disposables = new List<IDisposable>();

    public AutoSync(Harmony harmony, INetwork network, IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.harmony = harmony;
        this.network = network;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
        moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
    }

    public void Dispose()
    {
        disposables.ForEach(d => d.Dispose());
    }

    public void SyncProperty<T>(PropertyInfo property, Func<T, string> stringIdGetter) where T : class
    {
        var propertySync = new PropertySync(harmony, moduleBuilder, messageBroker, objectManager, network);

        propertySync.SyncProperty(property, stringIdGetter);

        disposables.Add(propertySync);
    }
}

public class PropertySync : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<PropertySync>();
    private readonly Harmony harmony;
    private readonly ModuleBuilder moduleBuilder;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private IDisposable handler;

    public PropertySync(
        Harmony harmony,
        ModuleBuilder moduleBuilder,
        IMessageBroker messageBroker,
        IObjectManager objectManager, 
        INetwork network)
    {
        this.harmony = harmony;
        this.moduleBuilder = moduleBuilder;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
    }

    public void Dispose()
    {
        handler?.Dispose();
    }

    public void SyncProperty<T>(PropertyInfo property, Func<T, string> stringIdGetterFn) where T : class
    {
        var setMethod = GetSetMethod(property);

        var dataClassType = GenerateDataClass(property.PropertyType, property.Name);
        var eventMessageType = GenerateEventMessage(dataClassType);
        var patchMethod = GeneratePatch(property, stringIdGetterFn, dataClassType, eventMessageType);
        var handlerType = GenerateHandler(property, eventMessageType);

        handler = (IDisposable)Activator.CreateInstance(handlerType, new object[] { messageBroker, objectManager, network, Logger, property });

        harmony.Patch(setMethod, prefix: new HarmonyMethod(patchMethod));
    }

    private MethodInfo GetSetMethod(PropertyInfo property)
    {
        var setMethod = property.GetSetMethod() ?? property.GetSetMethod(true);

        if (setMethod == null)
        {
            throw new Exception($"Property {property.Name} does not have a setter, try looking for where the data comes from in the property getter");
        }

        return setMethod;
    }

    private MethodInfo GeneratePatch<T>(PropertyInfo property, Func<T, string> stringIdGetterFn, Type dataType, Type eventType) where T : class
    {
        var patchGenerator = new HarmonyPatchGenerator(moduleBuilder, property, dataType, eventType);
        return patchGenerator.GenerateSetterPrefixPatch(property.GetSetMethod(), stringIdGetterFn);
    }

    private Type GenerateDataClass(Type dataType, string propertyName)
    {
        var dataClassGenerator = new DataClassGenerator(moduleBuilder);
        return dataClassGenerator.GenerateClass(dataType, propertyName);
    }

    private Type GenerateEventMessage(Type dataClassType)
    {
        var eventMessageGenerator = new EventMessageGenerator();
        return eventMessageGenerator.GenerateEvent(moduleBuilder, dataClassType);
    }

    private Type GenerateHandler(PropertyInfo property, Type eventMessageType)
    {
        return typeof(AutoSyncHandlerTemplate<,,>).MakeGenericType(property.DeclaringType, property.PropertyType, eventMessageType);
    }
}
