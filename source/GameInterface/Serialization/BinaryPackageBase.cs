using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Common.Extensions;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization
{
    public interface IBinaryPackage
    {
        void Pack();
        object Unpack(IBinaryPackageFactory binaryPackageFactory);
        T Unpack<T>(IBinaryPackageFactory binaryPackageFactory);
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

        [field: NonSerialized]
        public IBinaryPackageFactory BinaryPackageFactory
        {
            get;
            set;
        }

        protected Type ObjectType => typeof(T);

        /// <summary>
        /// Dictionary of field names and their objects converted to BinaryPackages
        /// </summary>
        protected Dictionary<string, IBinaryPackage> StoredFields = new Dictionary<string, IBinaryPackage>();

        protected BinaryPackageBase(T obj, IBinaryPackageFactory binaryPackageFactory)
        {
            if (obj == null) throw new ArgumentNullException();

            Object = obj;
            BinaryPackageFactory = binaryPackageFactory;
        }
        
        protected abstract void PackInternal();
        protected abstract void UnpackInternal();

        protected void PackFields()
        {
            // Iterate through all of the instance fields of the object's type
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields().GroupBy(o => o.Name).Select(g => g.First()))
            {
                // Get the value of the current field in the object
                // Add a binary package of the field value to the StoredFields collection
                object obj = field.GetValue(Object);
                StoredFields.Add(field.Name, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }
        
        protected void PackFields(HashSet<string> excludes)
        {
            // Iterate through all of the instance fields of the object's type, excluding any fields that are specified in the Excludes collection
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(excludes).GroupBy(o => o.Name).Select(g => g.First()))
            {
                // Get the value of the current field in the object
                // Add a binary package of the field value to the StoredFields collection
                object obj = field.GetValue(Object);
                StoredFields.Add(field.Name, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }
        
        protected void UnpackFields()
        {
            var type = Object.GetType();
            var fields = type.GetAllInstanceFields();

            foreach (string fieldName in StoredFields.Keys)
            {
                var field = fields.FirstOrDefault(f => f.Name.Equals(fieldName));

                if (type.IsValueType)
                {
                    object boxed = Object;
                    field.SetValue(boxed, StoredFields[fieldName].Unpack(BinaryPackageFactory));
                    Object = (T)boxed;
                }
                else
                {
                    field.SetValue(Object, StoredFields[fieldName].Unpack(BinaryPackageFactory));
                }
            }
        }

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
        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (IsUnpacked) return Object;

            BinaryPackageFactory = binaryPackageFactory;

            Object = CreateObject();

            IsUnpacked = true;

            UnpackInternal();

            return Object;
        }

        public CastType Unpack<CastType>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (CastType)Unpack(binaryPackageFactory);
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
        protected OutT ResolveId<OutT>(string id) where OutT : MBObjectBase
        {
            // Return if id is null
            if (id == null) return null;

            // Get the object with the specified id
            if (BinaryPackageFactory.ObjectManager.TryGetObject(id, out OutT resolvedObj) == false) return null;

            return resolvedObj;
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
        protected IEnumerable<OutT> ResolveIds<OutT>(string[] ids) where OutT : MBObjectBase
        {
            if (ids == null) return Array.Empty<OutT>();

            // Convert ids to instances using the MBObjectManager
            IEnumerable<OutT> values = ids.Select(id => ResolveId<OutT>(id));

            // Return the resolved instances
            return values.Where(v => v != null);
        }
    }
}
