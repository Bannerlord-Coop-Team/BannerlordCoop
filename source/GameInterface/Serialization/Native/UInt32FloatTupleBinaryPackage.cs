using System;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class UInt32FloatTupleBinaryPackage : IBinaryPackage
    {
        [NonSerialized]
        private Tuple<uint, float> Object;

        private uint Item1;
        private float Item2;

        public UInt32FloatTupleBinaryPackage(
            Tuple<uint, float> tuple,
            IBinaryPackageFactory binaryPackageFactory)
        {
            if (tuple == null) throw new ArgumentNullException(nameof(tuple));

            Object = tuple;
            Item1 = tuple.Item1;
            Item2 = tuple.Item2;
        }

        public void Pack() { }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            return Object ??= new Tuple<uint, float>(Item1, Item2);
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
