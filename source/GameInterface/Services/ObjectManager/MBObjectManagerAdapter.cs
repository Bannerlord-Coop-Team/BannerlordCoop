using Common.Extensions;
using GameInterface.Services.Clans;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ObjectManager;

public interface IObjectManager
{
    bool Contains(object obj);
    bool Contains(string id);
    bool TryGetObject<T>(string id, out T obj) where T : MBObjectBase;
    bool AddExisting(string id, object obj);
    bool AddNewObject(object obj, out string newId);
}

internal class MBObjectManagerAdapter : IObjectManager
{
    private MBObjectManager objectManager => MBObjectManager.Instance;

    private readonly IHeroRegistry heroRegistry;
    private readonly IMobilePartyRegistry partyRegistry;
    private readonly IClanRegistry clanRegistry;

    public MBObjectManagerAdapter(
        IHeroRegistry heroRegistry,
        IMobilePartyRegistry partyRegistry, 
        IClanRegistry clanRegistry)
    {
        this.heroRegistry = heroRegistry;
        this.partyRegistry = partyRegistry;
        this.clanRegistry = clanRegistry;
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

        return obj switch
        {
            MobileParty party => partyRegistry.RegisterExistingObject(id, party),
            Hero hero => heroRegistry.RegisterExistingObject(id, hero),
            Clan clan => clanRegistry.RegisterExistingObject(id, clan),
            _ => objectManager.RegisterPresumedObject(obj) != null,
        };
    }

    public bool AddNewObject(object obj, out string newId)
    {
        newId = null;

        if (objectManager == null) return false;
        if (obj is MBObjectBase mbObject == false) return false;

        return obj switch
        {
            MobileParty party => partyRegistry.RegisterNewObject(party, out newId),
            Hero hero => heroRegistry.RegisterNewObject(hero, out newId),
            Clan clan => clanRegistry.RegisterNewObject(clan, out newId),
            _ => AddNewObjectInternal(mbObject, out newId),
        };
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

        return obj switch
        {
            MobileParty party => partyRegistry.TryGetValue(party, out string _),
            Hero hero => heroRegistry.TryGetValue(hero, out string _),
            Clan clan => clanRegistry.TryGetValue(clan, out string _),
            _ => Contains(mbObject.StringId),
        };
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

        if (clanRegistry.TryGetValue(id, out Clan _))
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

    private static readonly MethodInfo GetObject = typeof(MBObjectManager)
        .GetMethod(nameof(MBObjectManager.GetObject), new Type[] { typeof(string) });
    public bool TryGetObject<T>(string id, out T obj) where T : MBObjectBase
    {
        obj = default;

        if (string.IsNullOrEmpty(id)) return false;
        if (objectManager == null) return false;

        if (partyRegistry.TryGetValue(id, out MobileParty party))
        {
            obj = party as T;
            return obj != null;
        }

        if (heroRegistry.TryGetValue(id, out Hero hero))
        {
            obj = hero as T;
            return obj != null;
        }

        if (clanRegistry.TryGetValue(id, out Clan clan))
        {
            obj = clan as T;
            return obj != null;
        }

        obj = (T)GetObject.MakeGenericMethod(typeof(T)).Invoke(objectManager, new object[] { id });

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
