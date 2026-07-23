using System;
using System.Collections;
using System.Collections.Generic;

namespace Common
{
    /// <summary>
    ///     Implements a drop out stack (circular array / ring buffer).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DropoutStack<T> : IEnumerable<T>
    {
        /// <summary>
        ///     Constructs a new stack with the given capacity.
        /// </summary>
        /// <param name="capacity"></param>
        public DropoutStack(int capacity)
        {
            m_Items = new T[capacity];
        }
        /// <summary>
        ///     Returns the number of items currently in the stack.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        ///     Returns an enumerator over the stack.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return GetItem(i);
            }
        }
        /// <summary>
        ///     Returns an enumerator over the stack.
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        /// <summary>
        ///     Adds an item to the front of the stack. If the stack is already at capacity, the tail of the stack is
        ///     removed.
        /// </summary>
        /// <param name="item"></param>
        public void Push(T item)
        {
            Count += Full ? 0 : 1;
            m_Items[m_IndexTop] = item;
            m_IndexTop = (m_IndexTop + 1) % m_Items.Length; // Circular buffer
        }
        /// <summary>
        ///     Removes an item from the front of the stack.
        /// </summary>
        /// <returns></returns>
        public T Pop()
        {
            Count -= Empty ? 0 : 1;
            m_IndexTop = (m_Items.Length + m_IndexTop - 1) % m_Items.Length;
            return m_Items[m_IndexTop];
        }
        /// <summary>
        ///     Peeks at the top item in the stack.
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            return m_Items[(m_Items.Length + m_IndexTop - 1) % m_Items.Length];
        }
        /// <summary>
        ///     Clears the 
        /// </summary>
        public void Clear()
        {
            Count = 0;

            if (!typeof(T).IsValueType)
            {
                // Free
                m_Items = new T[m_Items.Length];
            }
        }
        
        #region Private
        private T[] m_Items;
        private int m_IndexTop;
        private bool Full => Count == m_Items.Length;
        private bool Empty => Count == 0;
        private T GetItem(int index)
        {
            if (index > Count)
            {
                throw new InvalidOperationException("Index out of bounds");
            }

            return m_Items[(m_Items.Length + m_IndexTop - (index + 1)) % m_Items.Length];
        }
        #endregion
    }
}
