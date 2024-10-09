using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Tests.Stubs
{
    public class ObjectManagerAdapterStub : IObjectManager
    {
        private Dictionary<string, object> registry = new Dictionary<string, object>();

        private long objectCounter = 0;

        public bool AddExisting(string id, object obj)
        {
            if(registry.ContainsKey(id)) return false;

            registry.Add(id, obj);

            return true;
        }

        public bool AddNewObject(object obj, out string newId)
        {
            newId = string.Empty;

            if (registry.Values.Contains(obj)) return false;

            var generatedId = Interlocked.Increment(ref objectCounter);
            while (registry.ContainsKey(generatedId.ToString()))
            {
                generatedId = Interlocked.Increment(ref objectCounter);
            }

            newId = generatedId.ToString();

            registry.Add(newId, obj);

            return true;
        }

        public bool Contains(object obj)
        {
            return registry.Values.Contains(obj);
        }

        public bool Contains(string id)
        {
            return registry.ContainsKey(id);
        }

        public IEnumerable<T> GetObjectsOfType<T>()
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
            throw new NotImplementedException();
        }

        public bool IsTypeManaged(Type type)
        {
            throw new NotImplementedException();
        }

        public bool Remove(object obj)
        {
            throw new NotImplementedException();
        }

        public bool TryGetId(object obj, out string id)
        {
            throw new NotImplementedException();
        }

        public bool TryGetObject(string id, out object obj) => registry.TryGetValue(id, out obj);

        public bool TryGetObject<T>(string id, out T obj) where T : class
        {
            obj = default;

            if (TryGetObject(id, out object resolvedObject) == false) return false;
            if(resolvedObject is T castedObj == false) return false;
            
            obj = castedObj;
            
            return true;
        }
    }
}
