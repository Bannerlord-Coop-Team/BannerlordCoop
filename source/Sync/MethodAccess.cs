using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
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
            InitOriginal();
            if (MemberInfo.IsStatic)
            {
                m_CallStatic = InvokableFactory.CreateStaticStandInCaller(m_StandIn);
            }
            else
            {
                m_Call = InvokableFactory.CreateStandInCaller(m_StandIn);
            }
        }

        public MethodId Id { get; }

        public MethodInfo MemberInfo { get; }

        private void InitOriginal()
        {
            bool bHasPatches = Harmony.GetPatchInfo(MemberInfo) != null;
            HarmonyMethod standin = new HarmonyMethod(m_StandIn)
            {
                method = m_StandIn,
                reversePatchType = bHasPatches ?
                    HarmonyReversePatchType.Snapshot :
                    HarmonyReversePatchType.Original
            };
            Harmony.ReversePatch(MemberInfo, standin);
        }

        /// <summary>
        ///     Invokes the original method as it was at the time of creation of this
        ///     <see cref="MethodAccess" />.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="args"></param>
        public void CallOriginal([CanBeNull] object target, [CanBeNull] object[] args)
        {
            m_Call?.Invoke(target, args);
            m_CallStatic?.Invoke(args);
        }

        /// <summary>
        ///     Invokes registered handlers for the given instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="args"></param>
        /// <returns>true if a handler was invoked. False otherwise.</returns>
        public bool InvokeOnBeforeCallHandler([CanBeNull] object instance, params object[] args)
        {
            Action<object> handler = GetHandler(instance);
            handler?.Invoke(args);
            return handler == null;
        }

        public override string ToString()
        {
            return $"{MemberInfo.DeclaringType?.Name}.{MemberInfo.Name}";
        }
    }
}
