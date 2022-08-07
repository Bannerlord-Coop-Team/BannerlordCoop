using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Common
{
    /// <summary>
    /// Wrapper for Guid for referencing struct
    /// </summary>
    class GuidWrapper
    {
        public GuidWrapper(Guid guid)
        {
            Guid = guid;
        }

        public Guid Guid { get; set; }

    }

    /// <summary>
    /// Object manager for identifying objects across the network
    /// </summary>
    public class CoopObjectManager
    {
        public static event Action<Guid, object> NewObjectRegistered;

        public static readonly Dictionary<Guid, WeakReference<object>> Objects = new Dictionary<Guid, WeakReference<object>>();
        private static readonly ConditionalWeakTable<object, GuidWrapper> Guids = new ConditionalWeakTable<object, GuidWrapper>();
        private static readonly Dictionary<Type, List<Guid>> AssociatedGuids = new Dictionary<Type, List<Guid>>();
        
        private static void AddTypeEntry(object obj, Guid id)
        {
            Type type = obj.GetType();
            if (AssociatedGuids.ContainsKey(obj.GetType()))
            {
                AssociatedGuids[type].Add(id);
            }
            else
            {
                AssociatedGuids.Add(type, new List<Guid>(new[] { id }));
            }
        }

        public static bool ContainsElement(object obj)
        {
            GuidWrapper guidWrapper;
            Guids.TryGetValue(obj, out guidWrapper);

            return guidWrapper != null;
        }

        public static void Assert(Guid guid, object obj)
        {
            if(obj == null)
            {
                throw new ArgumentException($"Invalid object.");
            }
            if(guid == Guid.Empty)
            {
                throw new ArgumentException($"Invalid guid.");
            }

            if(Guids.TryGetValue(obj, out GuidWrapper guidWrapper))
            {
                if(guidWrapper.Guid == guid)
                {
                    // Already correct
                    return;
                }

                // Wrong guid for the object
                RemoveObject(obj);
            }
            AddObject(guid, obj);
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
            else
            {
                return AddObject(guid, obj);
            }
        }

        public static Guid RegisterNewObject(object obj)
        {
            if (obj == null) throw new ArgumentNullException();
            if (ContainsElement(obj)) throw new InvalidOperationException("Object was already registered.");
            return AddObject(obj);
        }

        private static bool AddObject(Guid guid, object obj)
        {
            if (ContainsElement(obj))
            {
                return false;
            }
            else if (Objects.TryGetValue(guid, out WeakReference<object> wp) &&
                     wp.TryGetTarget(out object existingObj))
            {
                throw new ArgumentException($"{guid} is already assigned to {existingObj}. Cannot add a different {obj} with the same guid.");
            }

            Guids.Add(obj, new GuidWrapper(guid));
            Objects.Add(guid, new WeakReference<object>(obj));
            AddTypeEntry(obj, guid);
            Task.Factory.StartNew(() => { NewObjectRegistered?.Invoke(guid, obj); });
            return true;
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
                return newId;
            }

            return GetGuid(obj);
        }

        public static bool TryGetObject<T>(Guid id, out T obj) where T : class
        {
            obj = GetObject(id) as T;
            if (obj == null) return false;
            return true;
        }

        public static bool TryGetObject(Guid id, out object obj)
        {
            obj = GetObject(id);
            if (obj == null) return false;
            return true;
        }

        public static object GetObject(Guid id)
        {
            if (id == null || id.Equals(Guid.Empty))
            {
                return null;
            }

            Objects.TryGetValue(id, out WeakReference<object> wp);
            if (wp == null)
            {
                return null;
            }

            wp.TryGetTarget(out object obj);
            if (obj == null)
            {
                // Expired, cleanup internal state
                RemoveObject(id);
            }
            return obj;
        }

        /// <summary>
        /// Gets the registered Guid for a given object.
        /// </summary>
        /// <param name="obj">Object to get Guid</param>
        /// <returns>Key of object, Null if object is not registered in object manager.</returns>
        public static bool TryGetGuid(object obj, out Guid guid)
        {
            guid = default;
            if (obj == null)
            {
                return false;
            }
            else if (ContainsElement(obj))
            {
                GuidWrapper guidWrapper;
                Guids.TryGetValue(obj, out guidWrapper);
                guid = guidWrapper.Guid;
                return true;
            }
            return false;
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
                GuidWrapper guidWrapper;
                Guids.TryGetValue(obj, out guidWrapper);
                return guidWrapper.Guid;
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
                    GuidWrapper guidWrapper;
                    Guids.TryGetValue(obj, out guidWrapper);
                    guids.Add(guidWrapper.Guid);
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

            WeakReference<object> wp = Objects[id];
            if(!wp.TryGetTarget(out object obj))
            {
                // Expired, cleanup internal state
                RemoveObject(id);
                return default(T);
            }
            if (!(obj is T))
            {
                throw new Exception("Stored object is not the same type as given type.");
            }
            return (T)obj;
        }

        public static IEnumerable<T> GetObjects<T>()
        {
            return AssociatedGuids[typeof(T)].Select(guid => (T)GetObject(guid));
        }

        public static Dictionary<Type, List<Guid>> GetAssociatedGuids()
        {
            return AssociatedGuids;
        }

        public static IEnumerable<Guid> GetTypeGuids<T>()
        {
            return AssociatedGuids[typeof(T)];
        }

        private static bool RemoveObjectFromType(Guid id, object obj)
        {
            bool result;

            if (AssociatedGuids.ContainsKey(obj.GetType()))
            {
                result = AssociatedGuids[obj.GetType()].Remove(id);
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

            if (Objects.ContainsKey(id))
            {
                WeakReference<object> wp = Objects[id];
                if(wp.TryGetTarget(out object obj))
                {
                    result = RemoveObject(obj);
                    RemoveObjectFromType(id, obj);
                }
                else
                {
                    // Object no longer exists. Cleanup internal state.
                    Objects.Remove(id);

                    // Since the object no longer exist, we have to manually search the AssociatedGuids
                    foreach (var pair in AssociatedGuids)
                    {
                        if(pair.Value.Remove(id))
                        {
                            // Assuming we only assigned each Guid once, this should be sufficient.
                            break;
                        }
                    }
                    result = true;
                }
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
                GuidWrapper guidWrapper;
                Guids.TryGetValue(obj, out guidWrapper);
                Guid id = guidWrapper.Guid;

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
