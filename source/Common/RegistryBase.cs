using System;
using System.Collections;
using System.Collections.Generic;

namespace Common
{
    public interface IRegistryBase<T> : IEnumerable<KeyValuePair<Guid, T>>
    {
        /// <summary>
        /// Count of registered objects
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Register an object with a new Guid
        /// </summary>
        /// <param name="item">Object to register with Guid</param>
        /// <returns>True if registration was successful, otherwise false</returns>
        bool RegisterExistingObject(Guid id, T item);

        /// <summary>
        /// Register an object with an existing Guid
        /// </summary>
        /// <param name="item">Id to associate object with</param>
        /// <param name="item">Object to register with Guid</param>
        /// <returns>True if registration was successful, otherwise false</returns>
        bool RegisterNewObject(T item);

        /// <summary>
        /// Remove a registered object from the registry.
        /// This will also remove the Guid.
        /// </summary>
        /// <param name="item">Object to remove from registry</param>
        /// <returns>True if removal was successful, otherwise false</returns>
        bool Remove(T item);

        /// <summary>
        /// Remove a Guid from the registry.
        /// This will also remove the object.
        /// </summary>
        /// <param name="id">Id to remove from registry</param>
        /// <returns>True if removal was successful, otherwise false</returns>
        bool Remove(Guid id);

        /// <summary>
        /// Getter for associated object id in the registry
        /// </summary>
        /// <param name="obj">Object to get id for</param>
        /// <param name="id">Stored id, will be default if no id/obj exists</param>
        /// <returns>True if retrieval was successful, otherwise false</returns>
        bool TryGetValue(T obj, out Guid id);

        /// <summary>
        /// Getter for associated object in the registry
        /// </summary>
        /// <param name="id">Id to get object for</param>
        /// <param name="obj">Stored obj, will be default if no id/obj exists</param>
        /// <returns>True if retrieval was successful, otherwise false</returns>
        bool TryGetValue(Guid id, out T obj);
    }

    /// <summary>
    /// Registry framework class for associating an object with a Guid.
    /// </summary>
    /// <typeparam name="T">Type to allow registering</typeparam>
    public abstract class RegistryBase<T> : IRegistryBase<T>
    {
        protected readonly TwoWayDictionary<Guid, T> _dictionary = new TwoWayDictionary<Guid, T>();

        /// <inheritdoc/>
        public int Count => _dictionary.Count;

        /// <inheritdoc/>
        public virtual bool RegisterNewObject(T item)
        {
            if (item == null) return false;

            return _dictionary.Add(Guid.NewGuid(), item);
        }

        /// <inheritdoc/>
        public virtual bool RegisterExistingObject(Guid id, T item) 
        {
            if (item == null) return false;

           return _dictionary.Add(id, item); 
        }

        /// <inheritdoc/>
        public virtual bool Remove(T item) 
        {
            if (item == null) return false;

            return _dictionary.Remove(item); 
        }

        /// <inheritdoc/>
        public virtual bool Remove(Guid id) => _dictionary.Remove(id);

        /// <inheritdoc/>
        public virtual bool TryGetValue(T obj, out Guid id)
        {
            if (obj == null) return false;

            return _dictionary.TryGetValue(obj, out id);
        }

        /// <inheritdoc/>
        public virtual bool TryGetValue(Guid id, out T obj) => _dictionary.TryGetValue(id, out obj);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<Guid, T>> GetEnumerator() => _dictionary.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();
    }
}
