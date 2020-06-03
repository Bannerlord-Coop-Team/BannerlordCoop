using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    /// <summary>
    ///     Patch generator for method calls.
    /// </summary>
    public class MethodPatch
    {
        private readonly List<MethodAccess> m_Access = new List<MethodAccess>();
        private readonly Type m_Declaring;

        public MethodPatch([NotNull] Type declaringClass)
        {
            m_Declaring = declaringClass;
        }

        public IEnumerable<MethodAccess> Methods => m_Access;

        ~MethodPatch()
        {
            foreach (MethodAccess syncMethod in m_Access)
            {
                MethodPatchFactory.RemovePrefix(syncMethod.MemberInfo);
            }
        }

        /// <summary>
        ///     Creates a <see cref="MethodAccess" /> and patches in a prefix that relays all calls to
        ///     <see cref="MethodAccess.RequestCall" />.
        /// </summary>
        /// <param name="method">Method to track.</param>
        /// <returns>this</returns>
        /// <exception cref="ArgumentException">
        ///     If the method is not declared in class
        ///     <see cref="m_Declaring" />
        /// </exception>
        public MethodPatch Relay(MethodInfo method)
        {
            if (method.DeclaringType != m_Declaring)
            {
                throw new ArgumentException(
                    $"Provided method {method} is not declared in {m_Declaring}",
                    nameof(method));
            }

            PatchPrefix(method);
            return this;
        }

        /// <summary>
        ///     Creates a <see cref="MethodAccess" /> and patches in a prefix that relays all calls to
        ///     <see cref="MethodAccess.RequestCall" />.
        /// </summary>
        /// <param name="sMethodName">Name of the method</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">If no method with that name exists.</exception>
        public MethodPatch Relay(string sMethodName)
        {
            MethodInfo method = AccessTools.Method(m_Declaring, sMethodName);
            if (method == null)
            {
                throw new ArgumentException(
                    $"Method {m_Declaring}.{sMethodName} not found.",
                    nameof(sMethodName));
            }

            PatchPrefix(method);
            return this;
        }

        public bool TryGetMethod(string sMethodName, out MethodAccess methodAccess)
        {
            return TryGetMethod(AccessTools.Method(m_Declaring, sMethodName), out methodAccess);
        }

        public bool TryGetMethod(MethodInfo methodInfo, out MethodAccess methodAccess)
        {
            methodAccess = m_Access.FirstOrDefault(m => m.MemberInfo.Equals(methodInfo));
            return methodAccess != null;
        }

        private void PatchPrefix(MethodInfo original)
        {
            MethodInfo dispatcher = AccessTools.Method(
                typeof(MethodPatch),
                nameof(DispatchCallRequest));
            m_Access.Add(MethodPatchFactory.AddPrefix(original, dispatcher));
        }

        private static bool DispatchCallRequest(
            MethodAccess methodAccess,
            object instance,
            params object[] args)
        {
            return methodAccess.RequestCall(instance, args);
        }
    }
}
