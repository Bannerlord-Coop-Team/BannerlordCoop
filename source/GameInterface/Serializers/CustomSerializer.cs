using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public abstract class CustomSerializer : ICustomSerializer
    {
        public Type ObjectType { get; private set; }
        public readonly Dictionary<FieldInfo, object> SerializableObjects = new Dictionary<FieldInfo, object>();
        public readonly Dictionary<FieldInfo, ICollection> Collections = new Dictionary<FieldInfo, ICollection>();

        [NonSerialized]
        public readonly List<FieldInfo> NonSerializableObjects = new List<FieldInfo>();
        [NonSerialized]
        public readonly List<FieldInfo> NonSerializableCollections = new List<FieldInfo>();

        protected CustomSerializer() { }

        protected CustomSerializer(object obj)
        {
            ObjectType = obj.GetType();
            FieldInfo[] fields = GetFields();
            foreach (FieldInfo field in fields)
            {
                if(!field.IsLiteral)
                {
                    // Is field collection
                    if(field.FieldType.GetInterface(nameof(ICollection)) != null)
                    {
                        // If collection is serializable add to Collections list
                        if(field.FieldType.IsSerializable &&
                           IsCollectionSerializableRecursive(field.FieldType))
                        {
                            Collections.Add(field, (ICollection)field.GetValue(obj));
                        }
                        // otherwise, add to NonSerializableCollections list
                        else
                        {
                            NonSerializableCollections.Add(field);
                        }
                        
                    }
                    else if (field.FieldType == typeof(Action))
                    {
                        NonSerializableObjects.Add(field);
                    }
                    else if (IsSerializable(field.FieldType))
                    {
                        SerializableObjects.Add(field, field.GetValue(obj));
                    }
                    else
                    {
                        NonSerializableObjects.Add(field);
                    }
                }
            }
        }

        /// <summary>
        /// Deserialized object
        /// </summary>
        /// <returns>New instantiated object</returns>
        public abstract object Deserialize();

        /// <summary>
        /// Assigns natively serializable fields
        /// </summary>
        /// <param name="newObj">Object to assign values</param>
        /// <returns>Object</returns>
        protected virtual object Deserialize(object newObj)
        {
            foreach (FieldInfo field in SerializableObjects.Keys)
            {
                field.SetValue(newObj, SerializableObjects[field]);
            }
            return newObj;
        }

        /// <summary>
        /// Any assigned Guids are replaced with actual object references.
        /// </summary>
        public abstract void ResolveReferenceGuids();

        /// <summary>
        /// Get all fields from type
        /// </summary>
        /// <returns>FieldInfo[]</returns>
        protected FieldInfo[] GetFields()
        {
            return GetFields(ObjectType);
        }

        private FieldInfo[] GetFields(Type ObjectType)
        {
            List<FieldInfo> fields = new List<FieldInfo>(ObjectType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            if (ObjectType.BaseType != null)
            {
                fields.AddRange(GetFields(ObjectType.BaseType));
            }
            return fields.ToArray();
        }

        /// <summary>
        /// Checks if collection has serializable elements
        /// </summary>
        /// <param name="type">Type from collection</param>
        /// <returns>If collection is completely serializable</returns>
        private bool IsCollectionSerializableRecursive(Type type)
        {

            List<Type> elementTypes = new List<Type>(type.GetGenericArguments());

            // Native arrays do not have generic arguments
            if (elementTypes.Count == 0 && type.IsArray)
            {
                elementTypes.Add(type.GetElementType());
            }

            // Return true if list is empty, but never should be empty
            // TODO add validate to result or change to false
            bool result = true;
            foreach(Type elementType in elementTypes)
            {
                if (elementType.GetInterface(nameof(ICollection)) != null)
                {
                    result &= IsCollectionSerializableRecursive(elementType);
                }
                else
                {
                    result &= IsSerializable(elementType);
                }
            }
            return result;
        }

        private bool IsSerializable(Type type)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}
