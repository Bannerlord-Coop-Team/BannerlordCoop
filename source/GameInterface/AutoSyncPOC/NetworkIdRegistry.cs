using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace GameInterface.AutoSyncPOC;

public interface INetworkIdRegistry
{
    bool IsTypeManaged(Type type);
    bool TryRegisterObject(ulong id, object obj, out ulong networkId);
    bool TryRegisterObject(object obj, out ulong networkId);
    bool TryUnregisterObject(ulong id);
    bool TryUnregisterObject(object obj);
    bool TryGetObject(ulong id, out object obj);
    bool TryGetId(object obj, out ulong id);
}

public sealed class NetworkIdRegistry : INetworkIdRegistry
{
    private readonly Dictionary<ulong, object> _byId = new();
    private readonly Dictionary<object, ulong> _byObj = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Type> _managedTypes = new();
    private readonly object _gate = new();

    private ulong _nextId = 0;

    public bool IsTypeManaged(Type type) => _managedTypes.Contains(type);

    public bool TryRegisterObject(ulong id, object obj, out ulong networkId)
    {
        networkId = 0;

        if (obj is null || id == 0)
            return false;

        lock (_gate)
        {
            if (_byId.ContainsKey(id))
                return false;

            if (_byObj.ContainsKey(obj))
                return false;

            _managedTypes.Add(obj.GetType());
            _byId[id] = obj;
            _byObj[obj] = id;

            if (id > _nextId)
                _nextId = id;

            networkId = id;
            return true;
        }
    }

    public bool TryRegisterObject(object obj, out ulong networkId)
    {
        networkId = 0;

        if (obj is null)
            return false;

        lock (_gate)
        {
            if (_byObj.ContainsKey(obj))
                return false;

            ulong id;
            do
            {
                if (_nextId == ulong.MaxValue)
                    return false;

                id = ++_nextId;
            }
            while (_byId.ContainsKey(id));

            _managedTypes.Add(obj.GetType());
            _byId[id] = obj;
            _byObj[obj] = id;

            networkId = id;
            return true;
        }
    }

    public bool TryUnregisterObject(ulong id)
    {
        if (id == 0)
            return false;

        lock (_gate)
        {
            if (!_byId.TryGetValue(id, out var obj))
                return false;

            _byId.Remove(id);
            _byObj.Remove(obj);
            return true;
        }
    }

    public bool TryUnregisterObject(object obj)
    {
        if (obj is null)
            return false;

        lock (_gate)
        {
            if (!_byObj.TryGetValue(obj, out var id))
                return false;

            _byId.Remove(id);
            _byObj.Remove(obj);
            return true;
        }
    }

    public bool TryGetObject(ulong id, out object obj)
    {
        lock (_gate)
        {
            return _byId.TryGetValue(id, out obj!);
        }
    }

    public bool TryGetId(object obj, out ulong id)
    {
        id = 0;

        if (obj is null)
            return false;

        lock (_gate)
        {
            return _byObj.TryGetValue(obj, out id);
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}