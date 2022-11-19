using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serialization
{
    public interface IBinaryPackage
    {
        object Object { get; }
        void Pack();
    }

    [Serializable]
    public abstract class BinaryPackageBase<T> : IBinaryPackage where T : class
    {
        public object Object
        {
            get
            {
                return Unpack();
            }
        }

        [NonSerialized]
        protected T _object;

        [NonSerialized]
        protected BinaryPackageFactory BinaryPackageFactory;

        protected Type ObjectType => typeof(T);

        protected BinaryPackageBase(T obj, BinaryPackageFactory binaryPackageFactory)
        {
            _object = obj;
            BinaryPackageFactory = binaryPackageFactory;
        }

        public abstract void Pack();
        public abstract T Unpack();
    }
}
