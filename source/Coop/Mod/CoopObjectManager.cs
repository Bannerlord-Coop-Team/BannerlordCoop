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
        private static HashSet<Type> PatchedTypes = new HashSet<Type>();

        public CoopObjectManager()
        {
            // Trigger events when necessary
            OnObjectCreatedFromServer += (id) => OnObjectCreated?.Invoke(id);
            OnObjectDestroyedFromServer += (id) => OnObjectCreated?.Invoke(id);
            OnObjectDestroyedFromServer += (id) => OnObjectDestroyed?.Invoke(id);
            OnObjectDestroyedOnClient += (id) => OnObjectDestroyed?.Invoke(id);

            // Assosiate relevent events
            NetworkedObjectObserver.OnObjectDestroyedFromServer += OnObjectDestroyedFromServer;
            NetworkedObjectObserver.OnObjectDestroyedOnClient += OnObjectDestroyedOnClient;
            ManagedClass.OnObjectCreatedOnClient += OnObjectCreatedOnClient;
        }

        public static bool PatchType<T>(Harmony harmony) where T : class
        {
            Type t = typeof(T);
            if (PatchedTypes.Contains(t))
            {
                return false;
            }

            ManagedClass.Patch<T>(harmony);
            PatchedTypes.Add(t);
            return true;
        }

        public T CreateObjectFromServer<T>(Guid guid, params object[] args)
        {
            NetworkedObjectObserver observer = NetworkedObjectObserver.AddNetworkObject<T>(guid, args);
            OnObjectCreatedFromServer?.Invoke(guid);

            return (T)observer.Reference.Target;
        }

        public bool DestroyObjectFromServer(Guid guid)
        {
            bool result = NetworkedObjectObserver.RemoveObserver(guid);
            OnObjectDestroyedFromServer?.Invoke(guid);

            return result;
        }

        /// <summary>
        /// Object received and created from server
        /// </summary>
        public Action<Guid> OnObjectCreatedFromServer;

        /// <summary>
        /// Object was requested to be destroyed from server, this is after the object has been destryoed
        /// </summary>
        public Action<Guid> OnObjectDestroyedFromServer;

        /// <summary>
        /// Object created on client
        /// </summary>
        public Action<Guid> OnObjectCreatedOnClient;

        /// <summary>
        /// Object destroyed on client
        /// </summary>
        public Action<Guid> OnObjectDestroyedOnClient;

        /// <summary>
        /// Object created (from client or server)
        /// </summary>
        public Action<Guid> OnObjectCreated;

        /// <summary>
        /// Object destroyed (from client or server)
        /// </summary>
        public Action<Guid> OnObjectDestroyed;
    }

    public class ManagedClass
    {
        static readonly BindingFlags All = BindingFlags.Public |
                                           BindingFlags.Instance |
                                           BindingFlags.NonPublic;

        public static Action<Guid> OnObjectCreatedOnClient;

        void GetAllPrivateMethods(Type type)
        {
            type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
        }

        static readonly HarmonyMethod cPrefix = new HarmonyMethod(typeof(ManagedClass), "ConstructorPrefix");
        static readonly HarmonyMethod cPostfix = new HarmonyMethod(typeof(ManagedClass), "ConstructorPostfix");

        static readonly HarmonyMethod fPostfix = new HarmonyMethod(typeof(ManagedClass), "FinalizerPostfix");

        public static void Patch<T>(Harmony harmony)
        {
            foreach (ConstructorInfo method in typeof(T).GetConstructors(All))
            {
                harmony.Patch(method, cPrefix, cPostfix);
            }

            MethodInfo destructor = typeof(T).GetMethod("Finalize",
                                                        BindingFlags.NonPublic |
                                                        BindingFlags.Instance |
                                                        BindingFlags.DeclaredOnly);

            if (destructor != null)
            {
                harmony.Patch(destructor, postfix: fPostfix);
            }
        }

        static void ConstructorPrefix(ref object __instance)
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame[] stackFrames = stackTrace.GetFrames();
            foreach(StackFrame stackFrame in stackFrames)
            {
                if(stackFrame.GetMethod() == NetworkedObjectObserver.CreateObjectMethod)
                {
                    return;
                }
            }

            AttachInstance(__instance);
        }

        static void ConstructorPostfix(ref object __instance)
        {

        }

        static void FinalizerPostfix(ref object __instance)
        {
            DetachInstance(__instance);
        }

        static void AttachInstance(object __instance)
        {
            Guid guid = new NetworkedObjectObserver(__instance).Guid;
            OnObjectCreatedOnClient?.Invoke(guid);
        }

        static void DetachInstance(object __instance)
        {
            NetworkedObjectObserver.RemoveObserver(__instance);
        }
    }

    public class NetworkedObjectObserver
    {
        #region Config
        public static readonly TimeSpan ManualGCRate = TimeSpan.FromSeconds(5);
        public static bool ManualGCEnabled = true;
        #endregion

        private static Dictionary<Guid, NetworkedObjectObserver> ObservedObjects = new Dictionary<Guid, NetworkedObjectObserver>();
        private static ConditionalWeakTable<object, NetworkedObjectObserver> ObjectsWithGUID = new ConditionalWeakTable<object, NetworkedObjectObserver>();



        public static readonly MethodInfo CreateObjectMethod = typeof(NetworkedObjectObserver).GetMethod("AddNetworkObject");

        public static Action<Guid> OnObjectDestroyedOnClient;
        public static Action<Guid> OnObjectDestroyedFromServer;

        #region Accessors
        public static NetworkedObjectObserver GetObserver(Guid guid)
        {
            return ObservedObjects[guid];
        }

        public static int ObjectCount()
        {
            return ObservedObjects.Count;
        }

        public static Guid GetGuid(object obj)
        {
            NetworkedObjectObserver observer;
            if (ObjectsWithGUID.TryGetValue(obj, out observer))
            {
                return observer.Guid;
            }

            return Guid.Empty;
        }

        public static NetworkedObjectObserver GetObserver(object obj)
        {
            NetworkedObjectObserver observer;
            if (ObjectsWithGUID.TryGetValue(obj, out observer))
            {
                return observer;
            }

            return null;
        }

        public static bool RemoveObserver(Guid guid)
        {
            NetworkedObjectObserver observer;
            if (ObservedObjects.TryGetValue(guid, out observer))
            {
                ObservedObjects.Remove(guid);
                ObjectsWithGUID.Remove(observer.Reference.Target);
                OnObjectDestroyedFromServer?.Invoke(guid);
            }

            return observer != null;
        }

        public static void RemoveObserver(object obj)
        {
            NetworkedObjectObserver observer;
            if (ObjectsWithGUID.TryGetValue(obj, out observer))
            {
                ObservedObjects.Remove(observer.Guid);
                ObjectsWithGUID.Remove(obj);
            }
        }
        #endregion


        public readonly Guid Guid = Guid.NewGuid();

        public readonly WeakReference Reference;

        public static NetworkedObjectObserver AddNetworkObject<T>(Guid guid, params object[] arguments)
        {
            T newObj = (T)Activator.CreateInstance(typeof(T), args: arguments);

            return new NetworkedObjectObserver(guid, newObj);
        }

        NetworkedObjectObserver(Guid guid, object obj)
        {
            Guid = guid;
            Reference = new WeakReference(obj);
            ObjectsWithGUID.Add(obj, this);
            ObservedObjects.Add(guid, this);
        }

        public NetworkedObjectObserver(object obj)
        {
            Reference = new WeakReference(obj);
            ObjectsWithGUID.Add(obj, this);
            ObservedObjects.Add(Guid, this);
        }

        #region GarbageCollection
        //static Task ManualGCTask = Task.Run(ManualGC);

        async static void ManualGC()
        {
            while (ManualGCEnabled)
            {
                await Task.Delay(ManualGCRate);

                IEnumerable<KeyValuePair<Guid, NetworkedObjectObserver>> deadObservers = ObservedObjects.AsParallel()
                    .Where(kvp => { return !kvp.Value.Reference.IsAlive; });

                foreach (var kvp in deadObservers)
                {
                    RemoveObserver(kvp.Value.Guid);
                    OnObjectDestroyedOnClient?.Invoke(kvp.Value.Guid);
                }
            }
        }
        #endregion
    }
}
