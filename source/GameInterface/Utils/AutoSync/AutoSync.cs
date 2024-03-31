using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.AutoSync.Dynamic;
using GameInterface.Utils.AutoSync.Template;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.Utils.AutoSync;

/// <summary>
/// Service for automatically syncing properties between server and clients
/// </summary>
public interface IAutoSync : IDisposable
{
    public ISyncResults SyncProperty<T>(PropertyInfo property, Func<T, string> stringIdGetter) where T : class;
}

/// <inheritdoc cref="IAutoSync"/>
internal class AutoSync : IAutoSync
{
    private readonly Harmony harmony;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ISerializableTypeMapper typeMapper;

    private readonly List<IDisposable> disposables = new List<IDisposable>();

    private static AssemblyName assemblyName => new AssemblyName("AutoSyncDynamicAssembly");
    private static readonly AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

    

    public AutoSync(Harmony harmony, INetwork network, IMessageBroker messageBroker, IObjectManager objectManager, ISerializableTypeMapper typeMapper)
    {
        this.harmony = harmony;
        this.network = network;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.typeMapper = typeMapper;
    }

    public void Dispose()
    {
        disposables.ForEach(d => d.Dispose());
    }

    public ISyncResults SyncProperty<T>(PropertyInfo property, Func<T, string> stringIdGetter) where T : class
    {
        var propertySync = new PropertySync(harmony, moduleBuilder, messageBroker, objectManager, network, typeMapper);

        var results = propertySync.SyncProperty(property, stringIdGetter);

        disposables.Add(propertySync);

        return results;
    }
}

/// <summary>
/// Handles synchronization of properties.
/// </summary>
public class PropertySync : IDisposable
{
    private static readonly Dictionary<PropertyInfo, ISyncResults> PatchedProperties = new();

    private static readonly ILogger Logger = LogManager.GetLogger<PropertySync>();
    private readonly Harmony harmony;
    private readonly ModuleBuilder moduleBuilder;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISerializableTypeMapper typeMapper;
    private IAutoSyncHandlerTemplate handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertySync"/> class.
    /// </summary>
    /// <param name="harmony">The Harmony instance.</param>
    /// <param name="moduleBuilder">The module builder.</param>
    /// <param name="messageBroker">The message broker.</param>
    /// <param name="objectManager">The object manager.</param>
    /// <param name="network">The network.</param>
    /// <param name="typeMapper">The type mapper.</param>
    public PropertySync(
        Harmony harmony,
        ModuleBuilder moduleBuilder,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISerializableTypeMapper typeMapper)
    {
        this.harmony = harmony;
        this.moduleBuilder = moduleBuilder;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.typeMapper = typeMapper;
    }

    /// <summary>
    /// Disposes the handler.
    /// </summary>
    public void Dispose()
    {
        handler?.Dispose();
    }

    /// <summary>
    /// Synchronizes a property.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="property">The property to synchronize.</param>
    /// <param name="stringIdGetterFn">The function to get the string ID.</param>
    /// <returns>The synchronization results.</returns>
    public ISyncResults SyncProperty<T>(PropertyInfo property, Func<T, string> stringIdGetterFn) where T : class
    {
        if (PatchedProperties.TryGetValue(property, out var existingResult))
        {
            return existingResult;
        }

        var setMethod = GetSetMethod(property);

        var dataClassType = GenerateDataClass(property.PropertyType, property.Name);
        var eventMessageType = GenerateEventMessage(property);
        var networkMessageType = GenerateNetworkMessage(property);
        var patchMethod = GeneratePatch(property, stringIdGetterFn, dataClassType, eventMessageType);
        var handlerType = GenerateHandler(property, networkMessageType, eventMessageType);

        handler = (IAutoSyncHandlerTemplate)Activator.CreateInstance(handlerType, new object[] { messageBroker, objectManager, network, Logger, property });

        var serializableTypes = new Type[]
        {
            dataClassType,
            eventMessageType,
            networkMessageType,
        };

        typeMapper.AddTypes(serializableTypes);

        harmony.Patch(setMethod, prefix: new HarmonyMethod(patchMethod));

        var result = new SyncResults
        {
            DataType = dataClassType,
            EventType = eventMessageType,
            NetworkMessageType = networkMessageType,
            HandlerType = handlerType,
            SerializableTypes = serializableTypes,
        };

        PatchedProperties.Add(property, result);

        return result;
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

    private Type GenerateEventMessage(PropertyInfo property)
    {
        var eventMessageGenerator = new EventMessageGenerator();
        return eventMessageGenerator.GenerateEvent(moduleBuilder, property);
    }

    private Type GenerateNetworkMessage(PropertyInfo property)
    {
        var networkMessageGenerator = new NetworkMessageGenerator();
        return networkMessageGenerator.GenerateNetworkMessage(moduleBuilder, property);
    }

    private Type GenerateHandler(PropertyInfo property, Type networkMessageType, Type eventMessageType)
    {
        return typeof(AutoSyncHandler<,,,>).MakeGenericType(property.DeclaringType, property.PropertyType, networkMessageType, eventMessageType);
    }
}