using System;
using System.Collections.Generic;

namespace Common;


/// <inheritdoc cref="IRegistry"/>
/// <typeparam name="T">Object type to store</typeparam>
public interface IRegistry<T> : IEnumerable<KeyValuePair<string, T>>, IRegistry
{
}

/// <summary>
/// Registry for storing objects with an associated identifier
/// </summary>
public interface IRegistry
{
    /// <summary>
    /// Count of registered objects
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Type the registry manages
    /// </summary>
    Type ManagedType { get; }

    /// <summary>
    /// Register an object with a new <see cref="ObjectId"/>
    /// </summary>
    /// <param name="obj">Object to register with new <see cref="ObjectId"/></param>
    /// <returns>True if registration was successful, otherwise false</returns>
    bool RegisterNewObject(object obj, out string id);

    /// <summary>
    /// Register an object with an existing <see cref="ObjectId"/>
    /// </summary>
    /// <param name="id">Id to associate object with</param>
    /// <param name="obj">Object to register with <see cref="ObjectId"/></param>
    /// <returns>True if registration was successful, otherwise false</returns>
    bool RegisterExistingObject(string id, object obj);

    /// <summary>
    /// Remove a registered object from the registry.
    /// This will also remove the <see cref="ObjectId"/>.
    /// </summary>
    /// <param name="item">Object to remove from registry</param>
    /// <returns>True if removal was successful, otherwise false</returns>
    bool Remove(object item);

    /// <summary>
    /// Remove a <see cref="ObjectId"/> from the registry.
    /// This will also remove the object.
    /// </summary>
    /// <param name="id">Id to remove from registry</param>
    /// <returns>True if removal was successful, otherwise false</returns>
    bool Remove(string id);

    /// <summary>
    /// Getter for associated object id in the registry
    /// </summary>
    /// <param name="obj">Object to get id for</param>
    /// <param name="id">Stored id, will be default if no id/obj exists</param>
    /// <returns>True if retrieval was successful, otherwise false</returns>
    bool TryGetValue(object obj, out string id);


    /// <summary>
    /// Getter for associated object in the registry
    /// </summary>
    /// <typeparam name="T">Type to resolve</typeparam>
    /// <param name="id">Id to get object for</param>
    /// <param name="obj">Stored obj, will be default if no id/obj exists</param>
    /// <returns>True if retrieval was successful, otherwise false</returns>
    bool TryGetValue<T>(string id, out T obj) where T : class;
}
