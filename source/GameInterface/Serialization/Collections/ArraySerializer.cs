using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serialization.Collections
{
    /// <summary>
    /// Custom array serializer for protobuf-net support
    /// </summary>
    /// <typeparam name="T">Array's underlying type</typeparam>
    [ProtoContract]
    public class ArraySerializer<T> : ICollectionSerializer<T[]>
    {
        [ProtoMember(1)]
        protected Queue<T> Values { get; }

        [ProtoMember(2)]
        protected List<bool> NullElements { get; }

        public ArraySerializer()
        {
            Values = new Queue<T>();
            NullElements = new List<bool>();
        }

        public void Pack(T[] values)
        {
            foreach (var value in values)
            {
                if (value == null)
                {
                    NullElements.Add(true);
                }
                else
                {
                    Values.Enqueue(value);
                    NullElements.Add(false);
                }
            }
        }

        public T[] Unpack()
        {
            List<T> newList = new List<T>();

            foreach (var isNull in NullElements)
            {
                if (isNull)
                {
                    newList.Add(default);
                }
                else
                {
                    newList.Add(Values.Dequeue());
                }
            }

            return newList.ToArray();
        }
    }
}
