using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using NLog;

namespace CoopFramework
{
    public static class CoopFramework
    {
        public static bool IsEnabled => m_IsCoopEnabled?.Invoke() ?? true;

        public static void InitPatches(Func<bool> isCoopEnabled)
        {
            m_IsCoopEnabled = isCoopEnabled;
            if (m_Initialized)
            {
                Logger.Error("CoopFramework.InitPatches can only be called once.");
                return;
            }
            
            IEnumerable<MethodInfo> patchInitializers =
                from t in Assembly.GetExecutingAssembly().GetTypes()
                from m in t.GetMethods()
                where m.IsDefined(typeof(PatchInitializerAttribute))
                select m;
            foreach (MethodInfo initializer in patchInitializers)
            {
                if (!initializer.IsStatic)
                {
                    throw new Exception("Invalid [PatchInitializer]. Has to be static.");
                }
                
                Logger.Info("Init patch {}", initializer.DeclaringType);
                initializer.Invoke(null, null);
            }
        }

        #region Private
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static bool m_Initialized = false;
        private static Func<bool> m_IsCoopEnabled;

        #endregion
    }
}