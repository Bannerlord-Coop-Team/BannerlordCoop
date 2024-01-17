using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ObjectManager.Extensions
{
    public static class MBObjectManagerExtensions
    {
        private static readonly ConditionalWeakTable<MBObjectManager, ObjectTypeRecordsFacade> objectManagerExtensionData = new();

        public static IEnumerable<MBObjectBase> GetAllObjects(this MBObjectManager objectManager)
        {
            return objectManager.GetFacade().GetAllObjects();
        }

        private static readonly MethodInfo _registerPresumedObject = typeof(MBObjectManagerAdapter).GetMethod(nameof(MBObjectManager.RegisterPresumedObject));
        public static void RegisterPresumedObject(this MBObjectManager objectManager, MBObjectBase obj)
        {
            _registerPresumedObject
                .MakeGenericMethod(obj.GetType())
                .Invoke(objectManager, new object[] { obj });
        }

        private static readonly MethodInfo _containsObject = typeof(MBObjectManagerAdapter).GetMethod(nameof(MBObjectManager.ContainsObject));
        public static bool ContainsObject(this MBObjectManager objectManager, MBObjectBase obj)
        {
            return (bool)_containsObject
                .MakeGenericMethod(obj.GetType())
                .Invoke(objectManager, new object[] { obj.StringId });
        }

        public static IEnumerable<MBObjectBase> GetObjectsOfType<T>(this MBObjectManager objectManager)
        {
            return objectManager.GetFacade().GetObjectsOfType<T>();
        }

        private static uint typeCounter = 100U;
        private static readonly MethodInfo _registerType = typeof(MBObjectManagerAdapter).GetMethod(nameof(MBObjectManager.RegisterType));
        public static void RegisterType(this MBObjectManager objectManager, Type type)
        {
            _registerType
                .MakeGenericMethod(type)
                .Invoke(objectManager, new object[] { type.Name, type.Name + 's', typeCounter++ });
        }

        public static bool ContainsType(this MBObjectManager objectManager, Type type)
        {
            return objectManager.GetFacade().ContainsType(type);
        }

        public static bool Contains(this MBObjectManager objectManager, string id)
        {
            return objectManager.GetFacade().Contains(id);
        }

        private static ObjectTypeRecordsFacade GetFacade(this MBObjectManager objectManager)
        {
            if (objectManagerExtensionData.TryGetValue(objectManager, out var facade) == false)
            {
                facade = new ObjectTypeRecordsFacade(objectManager);
                objectManagerExtensionData.Add(objectManager, facade);
            }

            return facade;
        }
    }

    internal class ObjectTypeRecordsFacade
    {
        private Dictionary<Type, ObjectTypeRecordFacade> Records { get => GetRecords(); }

        private readonly FieldInfo ObjectTypeRecords = typeof(MBObjectManager)
            .GetField("ObjectTypeRecords", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly MBObjectManager objectManager;

        public ObjectTypeRecordsFacade(MBObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        private Dictionary<Type, ObjectTypeRecordFacade> GetRecords()
        {
            var records = new Dictionary<Type, ObjectTypeRecordFacade>();
            foreach (var obj in (IEnumerable<object>)ObjectTypeRecords.GetValue(objectManager))
            {
                var type = obj.GetType();
                var handledType = type.GetGenericArguments()[0];

                records.Add(handledType, new ObjectTypeRecordFacade(obj));
            }

            return records;
        }

        public IEnumerable<MBObjectBase> GetAllObjects()
        {
            foreach(var typeRecord in Records.Values)
            {
                foreach(var obj in typeRecord.GetObjects())
                {
                    yield return obj;
                }
            }
        }

        public IEnumerable<MBObjectBase> GetObjectsOfType<T>()
        {
            if (Records.ContainsKey(typeof(T)) == false) return Array.Empty<MBObjectBase>();

            return Records[typeof(T)].GetObjects();
        }

        public bool ContainsType(Type type) => Records.ContainsKey(type);

        public bool Contains(string id)
        {
            return Records.Values.Any(record => record.Contains(id));
        }
    }

    internal class ObjectTypeRecordFacade
    {
        private static readonly Type ObjectTypeRecordType = typeof(MBObjectManager).GetNestedType("ObjectTypeRecord`1", BindingFlags.NonPublic);

        private readonly WeakReference refObjectTypeRecord;

        private readonly Type type;
        private readonly Type storedValueType;

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
            refObjectTypeRecord = new WeakReference(objectTypeRecord);

            type = objectTypeRecord.GetType();
            storedValueType = type.GetGenericArguments()[0];

            if (type.GetGenericTypeDefinition() != ObjectTypeRecordType)
            {
                throw new ArgumentException($"Type {type} was not of expected type {ObjectTypeRecordType}");
            }
        }

        public IEnumerable<string> GetKeys()
        {
            var objs = type.GetField("_registeredObjects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(refObjectTypeRecord.Target);

            var t1 = objs.GetType().GetGenericArguments()[0];
            var t2 = objs.GetType().GetGenericArguments()[1];

            var values = typeof(Dictionary<,>)
                .MakeGenericType(t1, t2)
                .GetProperty("Keys")
                .GetValue(objs);

            return (IEnumerable<string>)values;
        }

        public IEnumerable<MBObjectBase> GetValues()
        {
            var objs = type.GetField("_registeredObjects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(refObjectTypeRecord.Target);

            var t1 = objs.GetType().GetGenericArguments()[0];
            var t2 = objs.GetType().GetGenericArguments()[1];

            var values = typeof(Dictionary<,>)
                .MakeGenericType(t1, t2)
                .GetProperty("Values")
                .GetValue(objs);

            return (IEnumerable<MBObjectBase>)values;
        }

        public IEnumerable<MBObjectBase> GetObjects() => GetValues();

        public bool Contains(string id)
        {
            var objs = type.GetField("_registeredObjects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(refObjectTypeRecord.Target);

            var t1 = objs.GetType().GetGenericArguments()[0];
            var t2 = objs.GetType().GetGenericArguments()[1];

            return (bool)typeof(Dictionary<,>)
                .MakeGenericType(t1, t2)
                .GetMethod("ContainsKey")
                .Invoke(objs, new object[] { id });
        }
    }
}
