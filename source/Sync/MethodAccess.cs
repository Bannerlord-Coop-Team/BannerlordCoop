﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using Sync.Behaviour;
using Sync.Reflection;

namespace Sync
{
    /// <summary>
    ///     Type erased invocation wrapper for patched methods. On creation, a snapshot of the IL
    ///     representation of the method is stored internally. The snapshot includes all patches
    ///     to the function already applied. The snapshot can be called using <see cref="CallOriginal" />.
    /// </summary>
    public class MethodAccess : Tracker
    {
        [CanBeNull] private readonly Action<object, object[]> m_Call;
        [CanBeNull] private readonly Action<object[]> m_CallStatic;

        private readonly DynamicMethod m_StandIn;

        public MethodAccess([NotNull] MethodInfo info)
        {
            MemberInfo = info;
            Id = MethodRegistry.Register(this);
            m_StandIn = InvokableFactory.CreateStandIn(this);
            InitOriginal(m_StandIn);
            if (MemberInfo.IsStatic)
            {
                m_CallStatic = InvokableFactory.CreateStaticStandInCaller(m_StandIn);
            }
            else
            {
                m_Call = InvokableFactory.CreateStandInCaller(m_StandIn);
            }
        }

        public EMethodPatchFlag Flags { get; private set; } = EMethodPatchFlag.None;

        /// <summary>
        ///     If set, this function will be called before invoking any onBeforeCall handlers. If the
        ///     function evaluates to false, the onBeforeCall handlers will not be called.
        ///
        ///     The provided object is the instance the method is being called on. null for static methods.
        /// </summary>
        [CanBeNull]
        public Func<object, bool> ConditionIsPatchActive { get; set; }

        public MethodId Id { get; }

        public MethodInfo MemberInfo { get; }

        public void AddFlags(EMethodPatchFlag flag)
        {
            Flags |= flag;
        }

        private void InitOriginal(DynamicMethod toPatch)
        {
            lock (Patcher.HarmonyLock)
            {
                bool bHasPatches = Harmony.GetPatchInfo(MemberInfo) != null;
                HarmonyMethod standin = new HarmonyMethod(toPatch)
                {
                    method = m_StandIn,
                    reversePatchType = bHasPatches ?
                        HarmonyReversePatchType.Snapshot :
                        HarmonyReversePatchType.Original
                };
                Harmony.ReversePatch(MemberInfo, standin);
            }
        }

        /// <summary>
        ///     Invokes the method. Note that the prefixes decide on whether the original is called or not.
        ///     <see cref="MethodAccess" />.
        /// </summary>
        /// <param name="eOrigin">Who triggered this call?</param>
        /// <param name="instance"></param>
        /// <param name="args"></param>
        public void Call(ETriggerOrigin eOrigin, [CanBeNull] object instance, [CanBeNull] object[] args)
        {
            if (!InvokePrefix(eOrigin, instance, args)) return;
            m_Call?.Invoke(instance, args);
            m_CallStatic?.Invoke(args);
        }

        /// <summary>
        ///     Invokes registered handlers for the given instance.
        /// </summary>
        /// <param name="eOrigin">Who triggered this call?</param>
        /// <param name="instance"></param>
        /// <param name="args"></param>
        /// <returns>true if the invoked prefix wants the original function to be called as well. False otherwise.</returns>
        public bool InvokePrefix(ETriggerOrigin eOrigin, [CanBeNull] object instance, params object[] args)
        {
            if (ConditionIsPatchActive != null && !ConditionIsPatchActive(instance)) return true;

            var handler = GetHandler(instance);
            bool? doCallOriginal = handler?.Invoke(eOrigin, args);
            return doCallOriginal ?? true;
        }

        public override string ToString()
        {
            return $"{MemberInfo.DeclaringType?.Name}.{MemberInfo.Name}";
        }
    }
}
