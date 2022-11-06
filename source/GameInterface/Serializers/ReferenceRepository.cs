using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.LinQuick;

namespace GameInterface.Serializers
{
    public class ReferenceRepository
    {
        readonly Dictionary<int, object> IdToObj = new Dictionary<int, object>();
        readonly Dictionary<object, int> ObjToId = new Dictionary<object, int>();
        int RefCounter = 0;
        public int AddReference(object obj)
        {
            if(ObjToId.TryGetValue(obj, out int id))
            {
                return id;
            }
            
            var refId = Interlocked.Increment(ref RefCounter);
            AddValues(refId, obj);
            return refId;
        }

        public void RegisterInstance(int id, object obj)
        {
            AddValues(id, obj);
        }

        public bool TryGetObject(int id, out object obj)
        {
            return IdToObj.TryGetValue(id, out obj);
        }

        public bool TryGetId(object obj, out int id)
        {
            return ObjToId.TryGetValue(obj, out id);
        }

        private void AddValues(int id, object obj)
        {
            IdToObj.Add(id, obj);
            ObjToId.Add(obj, id);
        }
    }
}
