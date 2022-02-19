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
        private static readonly Dictionary<Guid, object> Objects = new Dictionary<Guid, object>();
        private static readonly Dictionary<object, Guid> Guids = new Dictionary<object, Guid>();
        private static readonly Dictionary<Type, List<Guid>> AssosiatedGuids = new Dictionary<Type, List<Guid>>();
        
        
        
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

        private static bool ContainsElement(object obj)
        {
            return Guids.ContainsKey(obj);
        }

        private static bool ContainsElement(Guid guid)
        {
            return Objects.ContainsKey(guid);
        }

        private static bool AddObject(Guid guid, object obj)
        {
            if (ContainsElement(obj))
            {
                return false;
            }
            else
            {
                Guids.Add(obj, guid);
                Objects.Add(guid, obj);
                return true;
            }
        }

        public static bool RegisterExistingObject(Guid guid, object obj)
        {
            if(obj == null)
            {
                if(guid != Guid.Empty)
                {
                    throw new ArgumentException($"Object not valid but guid is.");
                }
                return false;
            }
            else if(guid == Guid.Empty)
            {
                if(obj == null)
                {
                    throw new ArgumentException($"Guid not valid but object is.");
                }
                return false;
            }
            else if (ContainsElement(guid))
            {
                return false;
            }
            else if (ContainsElement(obj))
            {
                return false;
            }
            else
            {
                Objects.Add(guid, obj);
                return true;
            }
        }

        public static Guid AddObject(object obj)
        {
            if(obj == null)
            {
                return Guid.Empty;
            }

            Guid newId = Guid.NewGuid();

            if(AddObject(newId, obj))
            {
                AddTypeEntry(obj, newId);
                return newId;
            }

            return GetGuid(obj);
        }

        public static object GetObject(Guid id)
        {
            if (id == null || id.Equals(Guid.Empty))
            {
                return null;
            }

            Objects.TryGetValue(id, out object obj);

#if DEBUG
            if(obj == null)
            {
                CoopClient.Instance.Session.Connection.Send(
                    new Network.Protocol.Packet(
                        Network.Protocol.EPacket.BadID, 
                        Common.CommonSerializer.Serialize(id)) 
                    );
            }
#endif


            return obj;
        }


        /// <summary>
        /// Gets the registered Guid for a given object.
        /// </summary>
        /// <param name="obj">Object to get Guid</param>
        /// <returns>Key of object, Null if object is not registered in object manager.</returns>
        public static Guid GetGuid(object obj)
        {
            if(obj == null)
            {
                return Guid.Empty;
            }
            else if (ContainsElement(obj))
            {
                return Guids[obj];
            }
            return Guid.Empty;
        }

        /// <summary>
        /// Gets a list of guids for the given list of objects.
        /// </summary>
        /// <param name="obj">Object to get Guid</param>
        /// <returns>Keys of object enumerable, Null if object is not registered in object manager.</returns>
        public static IEnumerable<Guid> GetGuids(IEnumerable<object> listOfObjects)
        {
            List<Guid> guids = new List<Guid>();
            List<object> unresolvedObject = new List<object>();

            foreach (object obj in listOfObjects)
            {
                if (ContainsElement(obj))
                {
                    guids.Add(Guids[obj]);
                }
                else
                {
                    unresolvedObject.Add(obj);
                }
                
            }

            // Edge case where not all objects exist in object manager.
            if(unresolvedObject.Count > 0)
            {
                throw new Exception($"Not all objects have been registered by object manager. Objects in question {unresolvedObject}");
            }

            return guids;
        }

        public static T GetObject<T>(Guid id)
        {
            if(id == Guid.Empty || !Objects.ContainsKey(id))
            {
                return default(T);
            }

            object obj = Objects[id];
            if (obj.GetType() != typeof(T))
            {
                throw new Exception("Stored object is not the same type as given type.");
            }
            return (T)obj;
        }

        public static IEnumerable<T> GetObjects<T>()
        {
            return AssosiatedGuids[typeof(T)].Select(guid => (T)GetObject(guid));
        }

        public static IEnumerable<Guid> GetTypeGuids<T>()
        {
            return AssosiatedGuids[typeof(T)];
        }

        private static bool RemoveObjectFromType(Guid id, object obj)
        {
            bool result;

            if (AssosiatedGuids.ContainsKey(obj.GetType()))
            {
                result = AssosiatedGuids[obj.GetType()].Remove(id);
            }
            else
            {
                result = false;
            }

            return result;
        }

        public static bool RemoveObject(Guid id)
        {
            bool result;

            if (ContainsElement(id))
            {
                object obj = Objects[id];

                result = RemoveObject(obj);

                RemoveObjectFromType(id, obj);
            }
            else 
            {
                result = false; 
            }

            return result;            
        }

        public static bool RemoveObject(object obj)
        {
            bool result = true;

            if (ContainsElement(obj))
            {
                Guid id = Guids[obj];
                Guids.Remove(obj);
                Objects.Remove(id);

                RemoveObjectFromType(id, obj);
            }
            else
            {
                result = false;
            }

            return result;
        }
    }
}
