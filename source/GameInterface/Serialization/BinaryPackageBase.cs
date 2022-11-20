using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serialization
{
    public interface IBinaryPackage
    {
        void Pack();
        object Unpack();
        T Unpack<T>();
    }

    [Serializable]
    public abstract class BinaryPackageBase<T> : IBinaryPackage
    {
        [NonSerialized]
        private bool IsUnpacked = false;

        [NonSerialized]
        protected T Object;

        [NonSerialized]
        protected BinaryPackageFactory BinaryPackageFactory;

        protected Type ObjectType => typeof(T);

        /// <summary>
        /// Dictionary of fields and their objects converted to BinaryPackages
        /// </summary>
        protected Dictionary<FieldInfo, IBinaryPackage> StoredFields = new Dictionary<FieldInfo, IBinaryPackage>();

        protected BinaryPackageBase(T obj, BinaryPackageFactory binaryPackageFactory)
        {
            Object = obj;
            BinaryPackageFactory = binaryPackageFactory;
        }

        public abstract void Pack();
        public object Unpack()
        {
            if (IsUnpacked) return Object;

            Object = CreateObject();

            IsUnpacked = true;

            UnpackInternal();

            return Object;
        }

        public CastType Unpack<CastType>()
        {
            return (CastType)Unpack();
        }

        protected abstract void UnpackInternal();

        protected static T CreateObject()
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
    }
}
