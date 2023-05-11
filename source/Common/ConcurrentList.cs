using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    /// <summary>
    /// Thread safe list
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public class ConcurrentList<T> : IList<T>, ICollection<T>
    {
        public T this[int index]
        {
            get
            {
                lock (this)
                {
                    return _list[index];
                }
            }
            set
            {
                lock (this)
                {
                    _list[index] = value;
                }
            }
        }

        public int Count => _list.Count;

        public bool IsReadOnly => false;

        private readonly List<T> _list = new List<T>();

        public void Add(T item)
        {
            lock (this)
            {
                _list.Add(item);
            }
        }

        public void Clear()
        {
            lock (this)
            {
                _list.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (this)
            {
                return _list.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (this)
            {
                _list.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        public int IndexOf(T item)
        {
            lock (this)
            {
                return _list.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (this)
            {
                _list.Insert(index, item);
            }
        }

        public bool Remove(T item)
        {
            lock (this)
            {
                return _list.Remove(item);
            }
        }

        public void RemoveAt(int index)
        {
            lock (this)
            {
                _list.RemoveAt(index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
    }
}
