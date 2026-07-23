using System;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class NullBinaryPackage : IBinaryPackage
    {
        public void Pack()
        {
        }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            return null;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return default;
        }
    }
}
