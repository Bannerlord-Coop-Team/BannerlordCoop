using System;
using System.Collections;
using System.Collections.Generic;

namespace Common
{
    public class DropoutStack<T> : IEnumerable<T>
    {
        private readonly T[] m_Items;
        private int m_IndexTop;

        public DropoutStack(int capacity)
        {
            m_Items = new T[capacity];
        }

        private bool Full => Count == m_Items.Length;
        private bool Empty => Count == 0;

        public int Count { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return GetItem(i);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Push(T item)
        {
            Count += Full ? 0 : 1;
            m_Items[m_IndexTop] = item;
            m_IndexTop = (m_IndexTop + 1) % m_Items.Length; // Circular buffer
        }

        public T Pop()
        {
            Count -= Empty ? 0 : 1;
            m_IndexTop = (m_Items.Length + m_IndexTop - 1) % m_Items.Length;
            return m_Items[m_IndexTop];
        }

        public T Peek()
        {
            return m_Items[(m_Items.Length + m_IndexTop - 1) % m_Items.Length];
        }

        private T GetItem(int index)
        {
            if (index > Count)
            {
                throw new InvalidOperationException("Index out of bounds");
            }

            return m_Items[(m_Items.Length + m_IndexTop - (index + 1)) % m_Items.Length];
        }

        public void Clear()
        {
            Count = 0;
        }
    }
}
