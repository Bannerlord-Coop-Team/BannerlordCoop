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

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            return Object;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
