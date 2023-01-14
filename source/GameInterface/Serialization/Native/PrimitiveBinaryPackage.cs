using System;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class PrimitiveBinaryPackage : IBinaryPackage
    {
        object Object;

        public PrimitiveBinaryPackage(object @object)
        {
            Object = @object;
        }

        public void Pack() { }

        public object Unpack()
        {
            return Object;
        }

        public T Unpack<T>()
        {
            return (T)Unpack();
        }
    }
}
