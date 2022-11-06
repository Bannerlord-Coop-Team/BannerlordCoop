//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Runtime.Serialization;

//namespace GameInterface.Serializers
//{
//    public interface ICustomSerializer<T>
//    {
//        ICustomSerializer<T> Pack(T obj);
//        T Unpack();
//        void ResolveReferences(object obj);
//    }

//    [Serializable]
//    public abstract class CustomSerializerBase<T> : ICustomSerializer<T>
//    {
//        public readonly int ReferenceId;

//        [NonSerialized]
//        protected readonly SerializableFactory SerializableFactory;
//        [NonSerialized]
//        protected readonly ReferenceRepository ReferenceRepo;

//        public readonly Dictionary<FieldInfo, object> SerializableObjects = new Dictionary<FieldInfo, object>();
//        public readonly Dictionary<FieldInfo, ICollection> Collections = new Dictionary<FieldInfo, ICollection>();

//        [NonSerialized]
//        public readonly List<FieldInfo> NonSerializableObjects = new List<FieldInfo>();
//        [NonSerialized]
//        public readonly List<FieldInfo> NonSerializableCollections = new List<FieldInfo>();

//        protected CustomSerializerBase(SerializableFactory serializableFactory, ReferenceRepository referenceRepository)
//        {
//            SerializableFactory = serializableFactory;
//            ReferenceRepo = referenceRepository;

//            ReferenceId = ReferenceRepo.AddReference(this);
//        }


//        /// <summary>
//        /// Sorts and stores objects based on their serializability
//        /// </summary>
//        /// <param name="obj"></param>
//        protected virtual void CollectObjects(object obj)
//        {
//            FieldInfo[] fields = SerializationHelper.GetFields(typeof(T));
//            foreach (FieldInfo field in fields)
//            {
//                if (!field.IsLiteral)
//                {
//                    // Is field collection
//                    if (field.FieldType.GetInterface(nameof(ICollection)) != null)
//                    {
//                        // If collection is serializable add to Collections list
//                        if (field.FieldType.IsSerializable &&
//                           SerializationHelper.IsTypeSerializable(field.FieldType))
//                        {
//                            Collections.Add(field, (ICollection)field.GetValue(obj));
//                        }
//                        // otherwise, add to NonSerializableCollections list
//                        else
//                        {
//                            NonSerializableCollections.Add(field);
//                        }

//                    }
//                    else if (field.FieldType == typeof(Action))
//                    {
//                        NonSerializableObjects.Add(field);
//                    }
//                    else if (SerializationHelper.IsTypeSerializable(field.FieldType))
//                    {
//                        SerializableObjects.Add(field, field.GetValue(obj));
//                    }
//                    else
//                    {
//                        NonSerializableObjects.Add(field);
//                    }
//                }
//            }
//        }

//        public abstract ICustomSerializer<T> Pack(T obj);

//        public abstract T Unpack();
        
//        /// <summary>
//        /// Any assigned Guids are replaced with actual object references.
//        /// </summary>
//        public abstract void ResolveReferences(object obj);
//    }
//}
