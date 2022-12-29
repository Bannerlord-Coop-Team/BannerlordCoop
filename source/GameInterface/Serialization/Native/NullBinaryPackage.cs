using System;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class NullBinaryPackage : IBinaryPackage
    {
        public void Pack()
        {
        }

        public object Unpack()
        {
            return null;
        }

        public T Unpack<T>()
        {
            return default;
        }
    }
}
