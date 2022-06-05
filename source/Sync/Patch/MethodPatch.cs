using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using NLog;
using Sync.Behaviour;
using Sync.Call;

namespace Sync.Patch
{
    /// <summary>
    ///     Patch generator for method calls.
    /// </summary>
    public class MethodPatch<TPatch>
    {
        private const BindingFlags All = BindingFlags.Instance |
                                         BindingFlags.Static |
                                         BindingFlags.Public |
                                         BindingFlags.NonPublic |
                                         BindingFlags.GetField |
                                         BindingFlags.SetField |
                                         BindingFlags.GetProperty |
                                         BindingFlags.SetProperty;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<PatchedInvokable> m_Access = new List<PatchedInvokable>();
        protected readonly Type m_Declaring;

        public MethodPatch([NotNull] Type declaringClass)
        {
            m_Declaring = declaringClass;
        }

        public IEnumerable<PatchedInvokable> Methods => m_Access;

        /// <summary>
        ///     Patches all member methods of the declaring class with a prefix that relays all calls to
        ///     <see cref="PatchedInvokable.InvokePrefix" />.
        /// </summary>
        /// <param name="eBindingFlags"></param>
        /// <returns></returns>
        public MethodPatch<TPatch> InterceptAll(
            BindingFlags eBindingFlags =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
        {
            foreach (var method in m_Declaring.GetMethods(eBindingFlags)) Intercept(method);

            return this;
        }

        /// <summary>
        ///     Creates a <see cref="PatchedInvokable" /> and patches in a prefix that relays all calls to
        ///     <see cref="PatchedInvokable.InvokePrefix" />.
        /// </summary>
        /// <param name="method">Method to track.</param>
        /// <returns>this</returns>
        /// <exception cref="ArgumentException">
        ///     If the method is not declared in class
        ///     <see cref="m_Declaring" />
        /// </exception>
        public MethodPatch<TPatch> Intercept(
            MethodBase method)
        {
            if (method.DeclaringType != m_Declaring)
                throw new ArgumentException(
                    $"Provided method {method} is not declared in {m_Declaring}",
                    nameof(method));

            PatchPrefix(method);
            return this;
        }

        /// <summary>
        ///     Creates a <see cref="PatchedInvokable" /> and patches in a prefix that relays all calls to
        ///     <see cref="PatchedInvokable.InvokePrefix" />.
        /// </summary>
        /// <param name="sMethodName">Name of the method</param>
        /// <param name="eFlags">Flags for the generated interceptor.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">If no method with that name exists.</exception>
        public MethodPatch<TPatch> Intercept(
            string sMethodName,
            BindingFlags eBindingFlags = All)
        {
            foreach (var info in m_Declaring.GetMethods(eBindingFlags))
                if (info.Name == sMethodName)
                    Intercept(info);

            return this;
        }

        /// <summary>
        ///     Do not use, generics cannot be reliably patched as of right now. See
        ///     https://github.com/pardeike/Harmony/issues/320
        /// </summary>
        /// <param name="sMethodName"></param>
        /// <param name="genericInstantiations"></param>
        /// <param name="eBehaviour"></param>
        /// <returns></returns>
        [Obsolete]
        public MethodPatch<TPatch> InterceptGeneric(
            string sMethodName,
            Type[] genericInstantiations,
            BindingFlags eBindingFlags = All)
        {
            foreach (var info in m_Declaring.GetMethods(eBindingFlags))
                if (info.IsGenericMethod && info.Name == sMethodName)
                    foreach (var genericArg in genericInstantiations)
                        Intercept(info.MakeGenericMethod(genericArg));

            return this;
        }

        /// <summary>
        /// </summary>
        /// <param name="sMethodName"></param>
        /// <param name="eBindingFlags"></param>
        /// <returns></returns>
        public MethodPatch<TPatch> Postfix(
            string sMethodName,
            BindingFlags eBindingFlags = All)
        {
            foreach (var info in m_Declaring.GetMethods(eBindingFlags))
                if (info.Name == sMethodName)
                    Postfix(info);

            return this;
        }

        public MethodPatch<TPatch> Postfix(
            MethodBase method)
        {
            if (!m_Declaring.IsAssignableFrom(method.DeclaringType))
                throw new ArgumentException(
                    $"Provided method {method} is not declared in {m_Declaring}",
                    nameof(method));

            PatchPostfix(method);
            return this;
        }

        public MethodPatch<TPatch> AddFlags(
            MethodBase method,
            EInvokableFlag eFlags)
        {
            if (!TryGetMethod(method, out var access))
            {
                access = new PatchedInvokable(method, typeof(TPatch));
                m_Access.Add(access);
            }

            access.AddFlags(eFlags);
            return this;
        }

        public bool TryGetMethod(string sMethodName, out PatchedInvokable patchedInvokable)
        {
            var method = AccessTools.Method(m_Declaring, sMethodName);
            if (method.IsGenericMethod)
                throw new ArgumentException(
                    $"Unable to generate patch: provided method {method} is generic. Use a [HarmonyPatch] with TargetMethod instead.",
                    nameof(method));

            return TryGetMethod(method, out patchedInvokable);
        }

        public bool TryGetMethod(
            string sMethodName,
            Type[] genericArguments,
            out PatchedInvokable patchedInvokable)
        {
            var method = AccessTools.Method(
                m_Declaring,
                sMethodName,
                null,
                genericArguments);
            if (method.IsGenericMethod)
                throw new ArgumentException(
                    $"Unable to generate patch: provided method {method} is generic. Use a [HarmonyPatch] with TargetMethod instead.",
                    nameof(method));

            return TryGetMethod(method, out patchedInvokable);
        }

        public bool TryGetMethod(MethodBase methodInfo, out PatchedInvokable patchedInvokable)
        {
            patchedInvokable = m_Access.FirstOrDefault(m => m.Original.Equals(methodInfo));
            return patchedInvokable != null;
        }

        /// <summary>
        ///     Dynamically creates a new prefix for a call to <paramref name="original" /> that redirects the call
        ///     to our static dispatcher <see cref="DispatchPrefixExecution" />.
        /// </summary>
        /// <param name="original">Method to be patched.</param>
        private void PatchPrefix(
            MethodBase original)
        {
            Logger.Debug("{original} is being prefixed by {patcher}", original, typeof(TPatch));
            var dispatcher = AccessTools.Method(
                typeof(MethodPatch<TPatch>),
                nameof(DispatchPrefixExecution));
            if (!TryGetMethod(original, out var access))
            {
                access = new PatchedInvokable(original, typeof(TPatch));
                m_Access.Add(access);
            }

            MethodPatchFactory<TPatch>.AddPrefix(access, dispatcher);
        }

        /// <summary>
        ///     Dispatcher that is being called for prefixes to forward the call to <see cref="PatchedInvokable.InvokePrefix" />.
        /// </summary>
        /// <param name="patchedInvokable">Access to the patched method that is being called.</param>
        /// <param name="instance">Instance that the method is being called on.</param>
        /// <param name="args">Parameters to the method call.</param>
        /// <returns></returns>
        private static bool DispatchPrefixExecution(
            PatchedInvokable patchedInvokable,
            [CanBeNull] object instance,
            params object[] args)
        {
            return patchedInvokable.InvokePrefix(EOriginator.Game, instance, args);
        }

        private void PatchPostfix(
            MethodBase original)
        {
            Logger.Debug("{original} is being postfixed by {patcher}", original, typeof(TPatch));
            var dispatcher = AccessTools.Method(
                typeof(MethodPatch<TPatch>),
                nameof(DispatchPostfixExecution));
            if (!TryGetMethod(original, out var access))
            {
                access = new PatchedInvokable(original, typeof(TPatch));
                m_Access.Add(access);
            }

            MethodPatchFactory<TPatch>.AddPostfix(access, dispatcher);
        }

        private static void DispatchPostfixExecution(
            PatchedInvokable patchedInvokable,
            [CanBeNull] object instance,
            params object[] args)
        {
            patchedInvokable.InvokePostfix(EOriginator.Game, instance, args);
        }
    }
}