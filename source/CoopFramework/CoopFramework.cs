using System;
using System.Linq;
using System.Reflection;
using NLog;

namespace CoopFramework
{
    public static class CoopFramework
    {
        public static bool IsEnabled => m_IsCoopEnabled?.Invoke() ?? true;

        public static IObjectManager ObjectManager { get; private set; }

        public static void InitPatches(IObjectManager objectManager, Func<bool> isCoopEnabled)
        {
            ObjectManager = objectManager;
            m_IsCoopEnabled = isCoopEnabled;
            if (m_Initialized)
            {
                Logger.Error("CoopFramework.InitPatches can only be called once.");
                return;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => PatchAssembly(args.LoadedAssembly);
            foreach (var assembly in loadedAssemblies) PatchAssembly(assembly);

            m_Initialized = true;
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