using System;
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
    public class MethodAccess
    {
        /// <summary>
        ///     Get the prefix for this method call.
        /// </summary>
        [NotNull] public Prefix Prefix { get; } = new Prefix();
        /// <summary>
        ///     Get the postfix for this method call.
        /// </summary>
        [NotNull] public Postfix Postfix { get; } = new Postfix();
        /// <summary>
        ///     The type that declared this invocation wrapper. That is usually the class that generated the patch.
        /// </summary>
        [NotNull] public Type DeclaringType { get; }

        /// <summary>
        ///     Creates a new invocation wrapper.
        /// </summary>
        /// <param name="info">The method that is being patched.</param>
        /// <param name="patcherType">The type info of the class that declared the patch. Used for debugging purposes.</param>
        public MethodAccess([NotNull] MethodBase info, [NotNull] Type patcherType)
        {
            MethodBase = info;
            DeclaringType = patcherType;
            Id = Registry.Register(this);
            m_StandIn = InvokableFactory.CreateStandIn(this);
            InitOriginal(m_StandIn);
            if (MethodBase.IsStatic)
            {
                m_CallStatic = InvokableFactory.CreateStaticStandInCaller(m_StandIn);
            }
            else
            {
                m_Call = InvokableFactory.CreateStandInCaller(m_StandIn);
            }
        }

        public EMethodPatchFlag Flags { get; private set; } = EMethodPatchFlag.None;

        public MethodId Id { get; }

        public MethodBase MethodBase { get; }

        public void AddFlags(EMethodPatchFlag flag)
        {
            Flags |= flag;
        }

        /// <summary>
        ///     Invokes the method. Note that the prefixes decide on whether the original is called or not.
        ///     <see cref="MethodAccess" />.
        /// </summary>
        /// <param name="eOrigin">Who triggered this call?</param>
        /// <param name="instance"></param>
        /// <param name="args"></param>
        public void Call(EOriginator eOrigin, [CanBeNull] object instance, [CanBeNull] object[] args)
        {
            if (!InvokePrefix(eOrigin, instance, args)) return;
            CallOriginal(instance, args);
        }

        /// <summary>
        ///     Calls the original function without the prefixes or applied.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="args"></param>
        public void CallOriginal([CanBeNull] object instance, [CanBeNull] object[] args)
        {
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
        public bool InvokePrefix(EOriginator eOrigin, [CanBeNull] object instance, params object[] args)
        {
            var handler = Prefix.GetHandler(instance);
            if (handler != null)
            {
                // Handler decides whether to call the original or not
                return handler.Invoke(eOrigin, args) == ECallPropagation.CallOriginal;
            }
            
            // Default when no handler is registered: Call original
            return true;
        }
        
        public void InvokePostfix(EOriginator eOrigin, object instance, object[] args)
        {
            var handler = Postfix.GetHandler(instance);
            handler?.Invoke(eOrigin, args);
        }

        public override string ToString()
        {
            return $"{MethodBase.DeclaringType?.Name}.{MethodBase.Name}";
        }
        
        #region Private
        private void InitOriginal(DynamicMethod toPatch)
        {
            lock (Patcher.HarmonyLock)
            {
                bool bHasPatches = Harmony.GetPatchInfo(MethodBase) != null;
                HarmonyMethod standin = new HarmonyMethod(toPatch)
                {
                    method = m_StandIn,
                    reversePatchType = bHasPatches ?
                        HarmonyReversePatchType.Snapshot :
                        HarmonyReversePatchType.Original
                };
                Harmony.ReversePatch(MethodBase, standin);
            }
        }
        
        [CanBeNull] private readonly Action<object, object[]> m_Call;
        [CanBeNull] private readonly Action<object[]> m_CallStatic;

        private readonly DynamicMethod m_StandIn;
        #endregion
    }
}
