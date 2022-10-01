using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serialization.Collections
{

    [ProtoContract]
    public interface ICollectionSerializer<T>
    {
        void Pack(T values);
        T Unpack();
    }
}
