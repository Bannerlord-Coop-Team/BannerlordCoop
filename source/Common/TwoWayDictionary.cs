using System;
using System.Collections;
using System.Collections.Generic;

namespace Common
{
    /// <summary>
    /// A dictionary that allows O(1) indexing for keys and values.
    /// <remarks>
    /// NOTE: Data will be double vs a normal dictionary
    /// </remarks>
    /// </summary>
    /// <typeparam name="T1">Type 1</typeparam>
    /// <typeparam name="T2">Type 2</typeparam>
    public class TwoWayDictionary<T1, T2> : IEnumerable<KeyValuePair<T1, T2>>
    {
        private readonly Dictionary<T1, T2> _dictionary = new Dictionary<T1, T2>();
        private readonly Dictionary<T2, T1> _reverseDictionary = new Dictionary<T2, T1>();

        /// <summary>
        /// Add items to the two way dictionary
        /// </summary>
        /// <param name="item1">Item 1</param>
        /// <param name="item2">Item 2</param>
        /// <returns>True if addition was successful, otherwise false</returns>
        public bool Add(T1 item1, T2 item2)
        {
            if (item1 == null || item2 == null) return false;
            if (_dictionary.ContainsKey(item1)) return false;
            if (_reverseDictionary.ContainsKey(item2)) return false;

            _dictionary.Add(item1, item2);
            _reverseDictionary.Add(item2, item1);

            return true;
        }

        /// <summary>
        /// Removes item from dictionary along with
        /// associated value.
        /// </summary>
        /// <param name="key">Item to remove</param>
        /// <returns>True if removal was successful otherwise false</returns>
        public bool Remove(T1 key)
        {
            if (_dictionary.TryGetValue(key, out T2 value) == false) return false;

            return Remove(key, value);
        }

        /// <inheritdoc cref="Remove(T1)"/>
        public bool Remove(T2 value)
        {
            if (_reverseDictionary.TryGetValue(value, out T1 key) == false) return false;

            return Remove(key, value);
        }

        /// <summary>
        /// Determines if the item exists in the dictionary O(1)
        /// </summary>
        /// <param name="item">Item to check if it exists</param>
        /// <returns>True if item exists in dictionary otherwise false</returns>
        public bool Contains(T1 item) => _dictionary.ContainsKey(item);

        /// <inheritdoc cref="Contains(T1)"/>
        public bool Contains(T2 item) => _reverseDictionary.ContainsKey(item);

        public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
        {
            foreach (var kvp in _dictionary)
            {
                yield return kvp;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();

        public int Count
        {
            get
            {
                ValidateCount();

                return _dictionary.Count;
            }
        }

        /// <summary>
        /// Clears dictionary of all values
        /// </summary>
        public void Clear()
        {
            _dictionary.Clear();
            _reverseDictionary.Clear();
        }

        /// <summary>
        /// Getter for associated object
        /// </summary>
        /// <param name="item1">Object to get associated object from</param>
        /// <param name="item2">Stored object, will be default if no obj exists</param>
        /// <returns>True if retrieval was successful, otherwise false</returns>
        public bool TryGetValue(T1 item1, out T2 item2) => _dictionary.TryGetValue(item1, out item2);

        /// <inheritdoc cref="TryGetValue(T1, out T2)"/>
        public bool TryGetValue(T2 item1, out T1 item2) => _reverseDictionary.TryGetValue(item1, out item2);

        public T2 this[T1 value]
        {
            get
            {
                return _dictionary[value];
            }
        }

        public T1 this[T2 value]
        {
            get
            {
                return _reverseDictionary[value];
            }
        }

        private bool Remove(T1 item1, T2 item2)
        {
            // Return failure if removed items do not exist in collections
            if (ContainsPair(item1, item2) == false) return false;

            return _dictionary.Remove(item1) &&
                   _reverseDictionary.Remove(item2);
        }

        private bool ContainsPair(T1 item1, T2 item2)
        {
            if (_dictionary.ContainsKey(item1) && _reverseDictionary.ContainsKey(item2))
            {
                return true;
            }

            // If pair only exists in one dictionary, data is now bad
            if (_dictionary.ContainsKey(item1))
            {
                throw new DataMisalignedException($"{item2} is missing from pair dictionary");
            }
            if (_reverseDictionary.ContainsKey(item2))
            {
                throw new DataMisalignedException($"{item1} is missing from pair dictionary");
            }

            return false;
        }

        private void ValidateCount()
        {
#if DEBUG
            if (_dictionary.Count != _reverseDictionary.Count)
            {
                throw new DataMisalignedException($"Dictionary counts should be equal but were not, " +
                    $"Dict Count: {_dictionary.Count}, Reverse dictionary count: {_reverseDictionary.Count}");
            }
#endif
        }
    }
}
