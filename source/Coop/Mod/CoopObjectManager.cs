using HarmonyLib;
using Network.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod
{
    public class CoopObjectManager
    {
        private static Dictionary<Guid, object> Objects = new Dictionary<Guid, object>();
        private static Dictionary<Type, List<Guid>> AssosiatedGuids = new Dictionary<Type, List<Guid>>();
        
        
        
        private static void AddTypeEntry(object obj, Guid id)
        {
            Type type = obj.GetType();
            if (AssosiatedGuids.ContainsKey(obj.GetType()))
            {
                AssosiatedGuids[type].Add(id);
            }
            else
            {
                AssosiatedGuids.Add(type, new List<Guid>(new[] { id }));
            }
        }

        public static void AddObject(object obj)
        {
            Guid newId = Guid.NewGuid();

            Objects.Add(newId, obj);

            AddTypeEntry(obj, newId);
        }

        public static object GetObject(Guid id)
        {
            return Objects[id];
        }

        public T GetObject<T>(Guid id)
        {
            object obj = Objects[id];
            if (obj.GetType() != typeof(T))
            {
                throw new Exception("Stored object is not the same type as given type.");
            }
            return (T)obj;
        }

        public static void RemoveObject(Guid id)
        {
            Objects.Remove(id);
        }
    }
}
