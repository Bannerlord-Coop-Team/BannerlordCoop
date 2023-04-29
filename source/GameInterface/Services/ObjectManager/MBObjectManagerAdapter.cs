using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.ObjectSystem;
using Common.Extensions;
using GameInterface.Services.GameDebug.Messages;
using System.Runtime.CompilerServices;
using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.ObjectManager
{
    public interface IObjectManager
    {
        bool Contains(object obj);
        bool Contains(string id);
        bool TryGetId(object obj, out string id);
        bool TryGetObject<T>(string id, out T obj);
        bool AddExisting(string id, object obj);
        bool AddNewObject(object obj, out string newId);
    }

    internal class MBObjectManagerAdapter : IObjectManager
    {
        private MBObjectManager objectManager => MBObjectManager.Instance;
        private readonly IHeroRegistry heroRegistry;
        private readonly IMobilePartyRegistry partyRegistry;

        public MBObjectManagerAdapter(IHeroRegistry heroRegistry, IMobilePartyRegistry partyRegistry)
        {
            this.heroRegistry = heroRegistry;
            this.partyRegistry = partyRegistry;
        }

        public bool AddExisting(string id, object obj)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (objectManager == null) return false;
            if (obj is MBObjectBase mbObject == false) return false;

            return AddExistingInternal(id, mbObject);
        }

        private bool AddExistingInternal<T>(string id, T obj) where T : MBObjectBase
        {
            if (string.IsNullOrEmpty(id)) return false;

            obj.StringId = id;

            if (obj is MobileParty party)
            {
                return partyRegistry.RegisterExistingObject(id, party);
            }

            if(obj is Hero hero)
            {
                return heroRegistry.RegisterExistingObject(id, hero);
            }

            T registeredObject = objectManager.RegisterPresumedObject(obj);
            if (registeredObject == null) return false;

            return true;
        }

        public bool AddNewObject(object obj, out string newId)
        {
            newId = null;

            if (objectManager == null) return false;
            if (obj is MBObjectBase mbObject == false) return false;

            if (obj is MobileParty party)
            {
                var result = partyRegistry.RegisterNewObject(party);
                newId = party.StringId;

                return result;
            }

            if (obj is Hero hero)
            {
                var result = heroRegistry.RegisterNewObject(hero);
                newId = hero.StringId;

                return result;
            }

            return AddNewObjectInternal(mbObject, out newId);
        }

        private bool AddNewObjectInternal<T>(T obj, out string id) where T : MBObjectBase
        {
            id = null;

            T registeredObject = objectManager?.RegisterObject(obj);

            if (registeredObject == null) return false;

            id = registeredObject.StringId;

            return true;
        }

        public bool Contains(object obj)
        {
            if (objectManager == null) return false;
            if (obj is MBObjectBase mbObject == false) return false;

            if (obj is MobileParty party)
            {
                return partyRegistry.TryGetValue(party, out string _);
            }

            if (obj is Hero hero)
            {
                return heroRegistry.TryGetValue(hero, out string _);
            }

            return Contains(mbObject.StringId);
        }

        
        public bool Contains(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (objectManager == null) return false;

            if(partyRegistry.TryGetValue(id, out MobileParty _))
            {
                return true;
            }

            if(heroRegistry.TryGetValue(id, out Hero _))
            {
                return true;
            }

            var adapterList = new ObjectTypeRecordsFacade(objectManager);

            return adapterList.Contains(id);
        }

        public bool TryGetId(object obj, out string id)
        {
            id = null;

            if (objectManager == null) return false;
            if (obj is MBObjectBase mbObject == false) return false;

            id = mbObject.StringId;

            return true;
        }

        public bool TryGetObject(string id, out object obj)
        {
            obj = default;

            if (string.IsNullOrEmpty(id)) return false;
            if (objectManager == null) return false;

            MBObjectBase castedResult;

            if (partyRegistry.TryGetValue(id, out MobileParty party))
            {
                obj = party;
                return true;
            }

            if (heroRegistry.TryGetValue(id, out Hero hero))
            {
                obj = hero;
                return true;
            }

            var result = TryGetObjectInternal(id, out castedResult);

            obj = castedResult;

            return result;
        }

        public bool TryGetObject<T>(string id, out T obj)
        {
            obj = default;

            if(TryGetObject(id, out object resolvedObj))
            {
                if (resolvedObj is T castedObj == false) return false;

                obj = castedObj;
                return true;
            }

            return false;
        }

        private bool TryGetObjectInternal<T>(string id, out T obj) where T : MBObjectBase
        {
            obj = default;

            if (string.IsNullOrEmpty(id)) return false;

            obj = objectManager.GetObject<T>(id);

            return obj != null;
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
