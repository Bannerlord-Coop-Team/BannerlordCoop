using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serialization.Collections
{
    /// <summary>
    /// Custom list serializer for protobuf-net support
    /// </summary>
    /// <typeparam name="T">List's underlying type</typeparam>
    [ProtoContract]
    public class ListSerializer<T> : ICollectionSerializer<List<T>>
    {
        [ProtoMember(1)]
        protected Queue<T> Values { get; }

        [ProtoMember(3)]
        protected List<bool> NullElements { get; }

        public ListSerializer()
        {
            Values = new Queue<T>();
            NullElements = new List<bool>();
        }

        public void Pack(List<T> values)
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

        public List<T> Unpack()
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

            return newList;
        }
    }
}
