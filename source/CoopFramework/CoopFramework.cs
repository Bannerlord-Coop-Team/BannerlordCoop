using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using NLog;
using TaleWorlds.SaveSystem;

namespace CoopFramework
{
    /// <summary>
    ///     Main entry point to start up the coop framework.
    /// </summary>
    public static class CoopFramework
    {
        /// <summary>
        ///     Evaluates whether the coop patches should be active or not.
        /// </summary>
        public static bool IsEnabled => m_IsCoopEnabled?.Invoke() ?? true;

        /// <summary>
        ///     Adapter for the games object manager.
        /// </summary>
        [CanBeNull] public static IObjectManager ObjectManager { get; private set; }

        internal static readonly Dictionary<Type,IObjectLifetimeObserver> MethodsByType = new Dictionary<Type, IObjectLifetimeObserver>();

        /// <summary>
        ///     Initializes all patches that are generated through <see cref="CoopManaged{TSelf,TExtended}"/>.
        ///     To be called once on start up after all assemblies that need patching are loaded.
        /// </summary>
        /// <param name="objectManager">Instance of the adapter to the games object manager.</param>
        /// <param name="isCoopEnabled">Function to evaluate whether the coop patches should be active or not.</param>
        public static void InitPatches([CanBeNull] IObjectManager objectManager, Func<bool> isCoopEnabled)
        {
            ObjectManager = objectManager;
            m_IsCoopEnabled = isCoopEnabled;
            if (m_Initialized)
            {
                Logger.Error("CoopFramework.InitPatches can only be called once.");
                return;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            //loadedAssemblies.ToList().ForEach(a =>
            //a.GetTypes().
            //Where(t => t.GetMethods(BindingFlags.Instance | | BindingFlags.Public | BindingFlags.NonPublic).
            //Any(m => m.GetCustomAttributes<LoadInitializationCallback>().Any())).ToList().ForEach(
            //    type => 
            //    LoadInitializationCallbacks.Add(
            //    type,
            //    type
            //    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            //    .Where(m => m.GetCustomAttributes<LoadInitializationCallback>().Any()).First())));
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => PatchAssembly(args.LoadedAssembly);
            foreach (var assembly in loadedAssemblies) PatchAssembly(assembly);

            m_Initialized = true;
        }
        /// <summary>
        /// This method tries to register an object to the CoopFramework. 
        /// If we have an observer for the type, then we register the object. 
        /// if we do not have an observer we return false.
        /// </summary>
        /// <param name="obj">The object we are trying to register.</param>
        /// <returns>the result of registering the object.</returns>
        public static bool TryRegister(object obj)
        {
            if (obj is null)
                return false;
            Type t = obj.GetType();
            if (MethodsByType.ContainsKey(t))
            {
                MethodsByType[t].AfterRegisterObject(obj);
                return true;
            }
            return false;
        }

        #region Private

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static bool m_Initialized;
        private static Func<bool> m_IsCoopEnabled;
        private static readonly object m_AssemblyPatchLock = new object();

        private static void PatchAssembly(Assembly assembly)
        {
            lock (m_AssemblyPatchLock)
            {
                foreach (var type in assembly.GetTypes()
                    .Where(t => !t.IsGenericType || !t.ContainsGenericParameters)
                ) // Cannot call methods on (partially) undefined generic types.
                foreach (var method in type.GetMethods(BindingFlags.Static |
                                                       BindingFlags.Public |
                                                       BindingFlags.NonPublic |
                                                       BindingFlags.FlattenHierarchy))
                    if (method.IsDefined(typeof(PatchInitializerAttribute)) &&
                        !method.GetCustomAttribute<PatchInitializerAttribute>().IsInitialized)
                    {
                        Logger.Info("Init patch {}.{}", method.DeclaringType, method.Name);
                        method.Invoke(null, null);
                        method.GetCustomAttribute<PatchInitializerAttribute>().IsInitialized = true;
                    }
            }
        }

        #endregion
    }
}