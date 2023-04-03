using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.ObjectSystem;
using Common.Extensions;
using GameInterface.Services.GameDebug.Messages;
using System.Runtime.CompilerServices;

namespace GameInterface.Services.ObjectManager
{
    public interface IObjectManagerAdapter<IdType>
    {
        bool Contains(object obj);
        bool Contains(IdType id);
        bool TryGetId(object obj, out IdType id);
        bool TryGetObject(IdType id, out object obj);
        bool AddExisting(IdType id, object obj);
        bool AddNewObject(object obj, out IdType newId);
    }

    public class MBObjectManagerAdapter : IObjectManagerAdapter<string>
    {
        private MBObjectManager objectManager => MBObjectManager.Instance;

        public bool AddExisting(string id, object obj)
        {
            if (objectManager == null) return false;

            if (obj is MBObjectBase mbObject == false) return false;

            return AddExistingInternal(id, mbObject);
        }

        private bool AddExistingInternal<T>(string id, T obj) where T : MBObjectBase
        {
            obj.StringId = id;

            T registeredObject = objectManager.RegisterPresumedObject(obj);

            if (registeredObject == null) return false;

            return true;
        }

        public bool AddNewObject(object obj, out string newId)
        {
            newId = string.Empty;

            if (objectManager == null) return false;

            if (obj is MBObjectBase mbObject == false) return false;

            return AddNewObjectInternal(mbObject, out newId);
        }

        private bool AddNewObjectInternal<T>(T obj, out string id) where T : MBObjectBase
        {
            id = string.Empty;

            T registeredObject = objectManager.RegisterObject(obj);

            if (registeredObject == null) return false;

            id = registeredObject.StringId;

            return true;
        }

        public bool Contains(object obj)
        {
            if (objectManager == null) return false;

            if (obj is MBObjectBase mbObject == false) return false;

            return Contains(mbObject.StringId);
        }

        
        public bool Contains(string id)
        {
            if (objectManager == null) return false;

            var adapterList = new ObjectTypeRecordsFacade(objectManager);

            return adapterList.Contains(id);
        }

        public bool TryGetId(object obj, out string id)
        {
            id = string.Empty;

            if (objectManager == null) return false;

            if (obj is MBObjectBase mbObject == false) return false;

            id = mbObject.StringId;

            return true;
        }

        public bool TryGetObject(string id, out object obj)
        {
            obj = default;

            if (objectManager == null) return false;

            MBObjectBase castedResult;

            var result = TryGetObjectInternal(id, out castedResult);

            obj = castedResult;

            return result;
        }

        public bool TryGetObjectInternal<T>(string id, out T obj) where T : MBObjectBase
        {
            obj = objectManager.GetObject<T>(id);

            return true;
        }
    }

    internal class ObjectTypeRecordsFacade
    {
        private static readonly Type ObjectTypeRecordType = typeof(MBObjectManager).GetNestedType("ObjectTypeRecord`1", BindingFlags.NonPublic);

        private readonly Dictionary<Type, ObjectTypeRecordFacade> records = new Dictionary<Type, ObjectTypeRecordFacade>();

        private readonly Func<MBObjectManager, IEnumerable<object>> ObjectTypeRecordsGetter = typeof(MBObjectManager)
            .GetField("ObjectTypeRecords", BindingFlags.NonPublic | BindingFlags.Instance)
            .BuildUntypedGetter<MBObjectManager, IEnumerable<object>>();
        private readonly MBObjectManager objectManager;

        private IEnumerable<ObjectTypeRecordFacade> objectTypeRecords { get
            {
                UpdateRecords();
                return records.Values;
            } 
        }

        public ObjectTypeRecordsFacade(MBObjectManager objectManager) 
        {
            this.objectManager = objectManager;

            UpdateRecords();
        }

        private void UpdateRecords()
        {
            foreach (var obj in ObjectTypeRecordsGetter(objectManager))
            {
                var type = obj.GetType();

                if (type != ObjectTypeRecordType) continue;

                var handledType = type.GetGenericArguments()[0];

                if (records.ContainsKey(handledType))
                {
                    if (records[handledType] == obj) continue;

                    records[handledType] = new ObjectTypeRecordFacade(obj);
                }

                records.Add(handledType, new ObjectTypeRecordFacade(obj));
            }
        }

        public bool Contains(string id)
        {
            return objectTypeRecords.Any(objectTypeRecord => objectTypeRecord.ContainsObject(id));
        }
    }

    internal class ObjectTypeRecordFacade
    {
        private static readonly Type ObjectTypeRecordType = typeof(MBObjectManager).GetNestedType("ObjectTypeRecord`1", BindingFlags.NonPublic);

        private readonly WeakReference refObjectTypeRecord;

        private readonly Func<object, string, bool> ContainsObjectDelegate;

        public object ObjectTypeRecord 
        { 
            get
            {
                if (refObjectTypeRecord.IsAlive == false)
                {
                    return null;
                }

                return refObjectTypeRecord.Target;
            } 
        }

        public ObjectTypeRecordFacade(object objectTypeRecord)
        {
            this.refObjectTypeRecord = new WeakReference(objectTypeRecord);

            Type type = objectTypeRecord.GetType();

            if (type.GetGenericTypeDefinition() != ObjectTypeRecordType)
            {
                throw new ArgumentException($"Type {type} was not of expected type {ObjectTypeRecordType}");
            }

            ContainsObjectDelegate = (Func<object, string, bool>)type
                .GetMethod("ContainsObject")
                .CreateDelegate(type);
        }

        public bool ContainsObject(string id)
        {
            var objectTypeRecord = ObjectTypeRecord;
            if (objectTypeRecord == null) return false;

            return ContainsObjectDelegate(objectTypeRecord, id);
        }
    }
}
