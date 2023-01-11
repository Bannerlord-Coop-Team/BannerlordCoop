using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization
{
    public interface IBinaryPackage
    {
        void Pack();
        object Unpack();
        T Unpack<T>();
    }

    /// <summary>
    /// A base class for creating binary packages for objects.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the object to be packed or unpacked.
    /// </typeparam>
    [Serializable]
    public abstract class BinaryPackageBase<T> : IBinaryPackage
    {
        [NonSerialized]
        private bool IsUnpacked = false;

        [NonSerialized]
        protected T Object;

        public BinaryPackageFactory BinaryPackageFactory
        {
            get { return _binaryPackageFactory; }
            set { _binaryPackageFactory = value; }
        }
        [NonSerialized]
        private BinaryPackageFactory _binaryPackageFactory;

        protected Type ObjectType => typeof(T);

        /// <summary>
        /// Dictionary of fields and their objects converted to BinaryPackages
        /// </summary>
        protected Dictionary<FieldInfo, IBinaryPackage> StoredFields = new Dictionary<FieldInfo, IBinaryPackage>();

        protected BinaryPackageBase(T obj, BinaryPackageFactory binaryPackageFactory)
        {
            if (obj == null) throw new ArgumentNullException();

            Object = obj;
            BinaryPackageFactory = binaryPackageFactory;
        }

        protected abstract void PackInternal();
        protected abstract void UnpackInternal();

        /// <summary>
        /// Packs data for an object.
        /// </summary>
        public void Pack()
        {
            PackInternal();
        }

        /// <summary>
        /// Unpacks the stored data for an object and returns the resulting object.
        /// </summary>
        /// <returns>
        /// The object created from the stored data.
        /// </returns>
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

        
        /// <summary>
        /// Packs a collection of objects into an array of string IDs.
        /// </summary>
        /// <typeparam name="InT">The type of the objects in the collection.</typeparam>
        /// <param name="values">The collection of objects to be packed.</param>
        /// <returns>An array of string IDs representing the objects in the collection.</returns>
        protected static string[] PackIds<InT>(IEnumerable<InT> values) where InT : MBObjectBase
        {
            // Return an empty array if the values parameter is null
            if (values == null) return new string[0];

            // Return an empty array if the values parameter is an empty collection
            if (!values.Any()) return new string[0];

            // Use the Select and ToArray LINQ methods to convert the values collection to an array of strings
            return values.Select(value => value.StringId).ToArray();
        }

        /// <summary>
        /// Resolves a string ID to an object of a specified type.
        /// </summary>
        /// <typeparam name="OutT">The type of the object to be resolved.</typeparam>
        /// <param name="id">The string ID to be resolved.</param>
        /// <returns>
        /// The object corresponding to the specified string ID, 
        /// or null if the ID is null or the object cannot be found.
        /// </returns>
        protected static OutT ResolveId<OutT>(string id) where OutT : MBObjectBase
        {
            // Return if id is null
            if (id == null) return null;

            // Get the character object with the specified id
            return MBObjectManager.Instance.GetObject<OutT>(id);
        }

        /// <summary>
        /// Resolves an array of string IDs to a collection of objects of a specified type.
        /// </summary>
        /// <typeparam name="OutT">The type of the objects to be resolved.</typeparam>
        /// <param name="ids">The array of string IDs to be resolved.</param>
        /// <returns>
        /// The collection of objects corresponding to the specified string IDs. 
        /// An exception is thrown if any of the IDs cannot be resolved.
        /// </returns>
        protected static IEnumerable<OutT> ResolveIds<OutT>(string[] ids) where OutT : MBObjectBase
        {
            // Convert ids to instances using the MBObjectManager
            IEnumerable<OutT> values = ids.Select(id => ResolveId<OutT>(id));

            // Return the resolved instances
            return values.Where(v => v != null);
        }
    }
}
