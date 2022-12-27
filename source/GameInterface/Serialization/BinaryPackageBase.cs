using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;

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

        protected abstract void PackInternal();
        protected abstract void UnpackInternal();


        public void Pack()
        {
            PackInternal();
        }
        
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

        protected static T CreateObject()
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        protected static OutT ResolveId<OutT>(string id) where OutT : MBObjectBase
        {
            // Return if id is null
            if (id == null) return null;

            // Get the character object with the specified id
            return MBObjectManager.Instance.GetObject<OutT>(id);
        }

        protected static IEnumerable<OutT> ResolveIds<OutT>(string[] ids) where OutT : MBObjectBase
        {
            // Convert ids to instances using the MBObjectManager
            IEnumerable<OutT> values = ids.Select(id => ResolveId<OutT>(id));

            // If any of the instances are null, throw an exception
            if (values.Any(v => v == null))
                throw new Exception($"Some values were not resolved in {values}");

            // Return the resolved instances
            return values;
        }
    }
}
