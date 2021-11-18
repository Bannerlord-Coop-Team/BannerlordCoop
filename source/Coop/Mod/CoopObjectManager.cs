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

        public static Guid AddObject(object obj)
        {
            Guid newId = Guid.NewGuid();

            Objects.Add(newId, obj);

            AddTypeEntry(obj, newId);

            return newId;
        }

        public static object GetObject(Guid id)
        {
#if DEBUG
            if (id == null || id.Equals(Guid.Empty))
            {
                throw new ArgumentException($"Guid is not valid: {id}");
            }
#endif
            return Objects[id];
        }


        /// <summary>
        /// SLOW - Try to avoid using when possible
        /// </summary>
        /// <param name="obj">Object to get Guid</param>
        /// <returns>Key of object, Null if object is not registered in object manager.</returns>
        public static Guid GetGuid(object obj)
        {
            return Objects.Single(x => x.Value == obj).Key;
        }

        /// <summary>
        /// SLOW - Try to avoid using when possible
        /// </summary>
        /// <param name="obj">Object to get Guid</param>
        /// <returns>Keys of object enumerable, Null if object is not registered in object manager.</returns>
        public static IEnumerable<Guid> GetGuids(IEnumerable<object> listOfObjects)
        {
            HashSet<object> hashSet = listOfObjects.ToHashSet();
            IEnumerable<Guid> result = Objects.AsParallel().Where(x => hashSet.Contains(x.Value)).Select(x => x.Key);

            // Edge case where not all objects exist in object manager.
            if(hashSet.Count != result.Count())
            {
                IEnumerable<object> exceptionResults = Objects.AsParallel().Where(x => !hashSet.Contains(x.Value)).Select(x => x.Value);
                throw new Exception($"Not all objects have been registered by object manager. Objects in question {exceptionResults}");
            }

            return result;
        }

        public static T GetObject<T>(Guid id)
        {
            object obj = Objects[id];
            if (obj.GetType() != typeof(T))
            {
                throw new Exception("Stored object is not the same type as given type.");
            }
            return (T)obj;
        }

        public static IEnumerable<T> GetObjects<T>()
        {
            return Objects.Where(x => AssosiatedGuids[typeof(T)]?.Contains(x.Key) ?? false).Select(kvp => kvp.Value).GetEnumerator() as IEnumerable<T>;
        }

        public static void RemoveObject(Guid id)
        {
            Objects.Remove(id);
        }
    }
}
