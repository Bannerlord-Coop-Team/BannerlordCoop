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
        private readonly List<MethodAccess> m_Sync = new List<MethodAccess>();

        public MethodPatcher([NotNull] Type declaringClass)
        {
            m_Declaring = declaringClass;
        }

        public IEnumerable<MethodAccess> Methods => m_Sync;

        ~MethodPatcher()
        {
            MethodInfo factoryMethod =
                typeof(MethodPatchFactory).GetMethod(nameof(MethodPatchFactory.GetPatch));
            foreach (MethodAccess syncMethod in m_Sync)
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

        public bool TryGetMethod(string sMethodName, out MethodAccess methodAccess)
        {
            return TryGetMethod(AccessTools.Method(m_Declaring, sMethodName), out methodAccess);
        }

        public bool TryGetMethod(MethodInfo methodInfo, out MethodAccess methodAccess)
        {
            methodAccess = m_Sync.FirstOrDefault(m => m.MemberInfo.Equals(methodInfo));
            return methodAccess != null;
        }

        private void PatchMethod(MethodInfo original)
        {
            m_Sync.Add(MethodPatchFactory.Patch(original));
        }

        public static bool DispatchCallRequest(
            MethodAccess methodAccess,
            object instance,
            params object[] args)
        {
            return methodAccess.RequestCall(instance, args);
        }
    }
}
