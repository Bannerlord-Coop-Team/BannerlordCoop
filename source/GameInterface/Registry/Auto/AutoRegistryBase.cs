using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.Registry.Auto;

public interface IAutoRegistry<T> where T : class
{
    /// <summary>
    /// Constructor methods that can be used to create instances of type T.
    /// </summary>
    /// <remarks>
    /// These methods will be patched to include logic for synchronizing object creation across the network.
    /// </remarks>
    IEnumerable<MethodBase> Constructors { get; }

    /// <summary>
    /// Destruction methods that can be used to destroy instances of type T.
    /// </summary>
    /// <remarks>
    /// These methods will be patched to include logic for synchronizing object destruction across the network.
    /// </remarks>
    IEnumerable<MethodBase> DestroyMethods { get; }

    /// <summary>
    /// Registers all available objects with the underlying system or service.
    /// </summary>
    void RegisterAllObjects();

    /// <summary>
    /// Handles logic to be executed after a client object has been created.
    /// </summary>
    /// <param name="obj">The client object instance that has been created. Cannot be null.</param>
    /// <param name="id">The unique identifier associated with the created client object. Cannot be null or empty.</param>
    void OnClientCreated(T obj, string id);

    /// <summary>
    /// Handles cleanup or additional processing when a client object is destroyed.
    /// </summary>
    /// <param name="obj">The client object instance that has been destroyed. Cannot be null.</param>
    /// <param name="id">The unique identifier associated with the destroyed client object. Cannot be null or empty.</param>
    void OnClientDestroyed(T obj, string id);

    /// <summary>
    /// Handles logic to be executed after a server object has been created.
    /// </summary>
    /// <param name="obj">The server object instance that was created. Cannot be null.</param>
    /// <param name="id">The unique identifier assigned to the created server object. Cannot be null or empty.</param>
    void OnServerCreated(T obj, string id);

    /// <summary>
    /// Handles logic to be performed when a server instance is destroyed.
    /// </summary>
    /// <param name="obj">The server object that has been destroyed. Represents the instance being removed.</param>
    /// <param name="id">The unique identifier of the server instance that was destroyed.</param>
    void OnServerDestroyed(T obj, string id);
}

public abstract class AutoRegistryBase<T> : IAutoRegistry<T> where T : class
{
    public virtual bool Debug { get; } = false;

    protected readonly ILogger Logger;

    protected readonly IObjectManager objectManager;
    public AutoRegistryBase(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
    {
        Logger = logger;
        this.objectManager = objectManager;
        autoRegistryFactory.AddRegistry(this);
    }

    /// <inheritdoc/>
    public abstract IEnumerable<MethodBase> Constructors { get; }

    /// <inheritdoc/>
    public abstract IEnumerable<MethodBase> DestroyMethods { get; }

    /// <inheritdoc/>
    public abstract void OnClientCreated(T obj, string id);

    /// <inheritdoc/>
    public abstract void OnClientDestroyed(T obj, string id);

    /// <inheritdoc/>
    public abstract void OnServerCreated(T obj, string id);

    /// <inheritdoc/>
    public abstract void OnServerDestroyed(T obj, string id);

    /// <inheritdoc/>
    public abstract void RegisterAllObjects();

    /// <summary>
    /// Registers an existing object with the specified identifier, associating it with the current object manager.
    /// </summary>
    /// <remarks>
    /// The identifier is prefixed with the type name to prevent conflicts between objects of different
    /// types. This method is intended for scenarios where objects are created externally and need to be tracked by the
    /// object manager.
    /// </remarks>
    /// <param name="id">The unique identifier to associate with the object. This value is used to distinguish the object within the manager
    /// and must not conflict with identifiers of other types.</param>
    /// <param name="obj">The object instance to register. Cannot be null.</param>
    protected void RegisterExistingObject(string id, T obj)
    {
        id = $"{typeof(T).Name}_{id}";

        objectManager.AddExisting(id, obj);
    }
}
