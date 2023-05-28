using System.Collections.Generic;

namespace Common
{
    public interface IRegistry<T> : IEnumerable<KeyValuePair<string, T>>
    {
        /// <summary>
        /// Count of registered objects
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Register an object with a new <see cref="ObjectId"/>
        /// </summary>
        /// <param name="obj">Object to register with new <see cref="ObjectId"/></param>
        /// <returns>True if registration was successful, otherwise false</returns>
        bool RegisterNewObject(T obj);

        /// <summary>
        /// Register an object with an existing <see cref="ObjectId"/>
        /// </summary>
        /// <param name="id">Id to associate object with</param>
        /// <param name="obj">Object to register with <see cref="ObjectId"/></param>
        /// <returns>True if registration was successful, otherwise false</returns>
        bool RegisterExistingObject(string id, T obj);



        /// <summary>
        /// Remove a registered object from the registry.
        /// This will also remove the <see cref="ObjectId"/>.
        /// </summary>
        /// <param name="item">Object to remove from registry</param>
        /// <returns>True if removal was successful, otherwise false</returns>
        bool Remove(T item);

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
        bool TryGetValue(T obj, out string id);

        /// <summary>
        /// Getter for associated object in the registry
        /// </summary>
        /// <param name="id">Id to get object for</param>
        /// <param name="obj">Stored obj, will be default if no id/obj exists</param>
        /// <returns>True if retrieval was successful, otherwise false</returns>
        bool TryGetValue(string id, out T obj);
    }


}
