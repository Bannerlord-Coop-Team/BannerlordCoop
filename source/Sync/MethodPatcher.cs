using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    public class MethodPatcher
    {
        private readonly Type m_Declaring;
        private readonly List<SyncMethod> m_Sync = new List<SyncMethod>();

        public MethodPatcher([NotNull] Type declaringClass)
        {
            m_Declaring = declaringClass;
        }

        public IEnumerable<SyncMethod> Methods => m_Sync;

        ~MethodPatcher()
        {
            MethodInfo factoryMethod =
                typeof(MethodPatchFactory).GetMethod(nameof(MethodPatchFactory.GetPatch));
            foreach (SyncMethod syncMethod in m_Sync)
            {
                MethodPatchFactory.Unpatch(syncMethod.MemberInfo);
            }
        }

        public MethodPatcher Synchronize(MethodInfo method)
        {
            PatchMethod(method);
            return this;
        }

        public MethodPatcher Synchronize(string sMethodName)
        {
            PatchMethod(AccessTools.Method(m_Declaring, sMethodName));
            return this;
        }

        public bool TryGetMethod(string sMethodName, out SyncMethod syncMethod)
        {
            return TryGetMethod(AccessTools.Method(m_Declaring, sMethodName), out syncMethod);
        }

        public bool TryGetMethod(MethodInfo methodInfo, out SyncMethod syncMethod)
        {
            syncMethod = m_Sync.FirstOrDefault(m => m.MemberInfo.Equals(methodInfo));
            return syncMethod != null;
        }

        private void PatchMethod(MethodInfo original)
        {
            m_Sync.Add(MethodPatchFactory.Patch(original));
        }

        public static bool DispatchCallRequest(
            SyncMethod method,
            object instance,
            params object[] args)
        {
            return method.RequestCall(instance, args);
        }
    }
}
