using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    public static class MethodPatchFactory
    {
        private static readonly Dictionary<MethodBase, DynamicMethod> m_Instances =
            new Dictionary<MethodBase, DynamicMethod>();

        public static MethodAccess Patch(MethodInfo original)
        {
            MethodAccess sync = new MethodAccess(original);
            if (m_Instances.ContainsKey(original))
            {
                throw new Exception("Patch already initialized.");
            }

            m_Instances[original] = CreatePrefix(sync);

            MethodInfo factoryMethod = typeof(MethodPatchFactory).GetMethod(nameof(GetPatch));

            HarmonyMethod patch = new HarmonyMethod(factoryMethod)
            {
                priority = SyncPriority.SyncCallPreUserPatch
            };
            Patcher.HarmonyInstance.Patch(original, patch);
            return sync;
        }

        public static void Unpatch(MethodInfo original)
        {
            MethodInfo factoryMethod = typeof(MethodPatchFactory).GetMethod(nameof(GetPatch));
            Patcher.HarmonyInstance.Unpatch(original, factoryMethod);
            m_Instances.Remove(original);
        }

        public static DynamicMethod GetPatch(MethodBase original)
        {
            return m_Instances[original];
        }

        private static DynamicMethod CreatePrefix(MethodAccess methodAccess)
        {
            List<SMethodParameter> parameters = methodAccess.MemberInfo.GetParameters()
                                                            .Select(
                                                                p => new SMethodParameter
                                                                {
                                                                    Info = p,
                                                                    ParameterType =
                                                                        p.ParameterType,
                                                                    Name = p.Name
                                                                })
                                                            .ToList();
            if (!methodAccess.MemberInfo.IsStatic)
            {
                parameters.Insert(
                    0,
                    new SMethodParameter
                    {
                        Info = null,
                        ParameterType = methodAccess.MemberInfo.DeclaringType,
                        Name = "__instance"
                    }); // Inject an __instance
            }

            DynamicMethod dyn = new DynamicMethod(
                "Prefix",
                typeof(bool),
                parameters.Select(p => p.ParameterType).ToArray(),
                methodAccess.MemberInfo.DeclaringType,
                true);

            for (int i = 0; i < parameters.Count; ++i)
            {
                SMethodParameter parameter = parameters[i];
                ParameterAttributes attr;
                if (parameter.Info != null)
                {
                    attr = parameter.Info.Attributes;
                }
                else
                {
                    // Injected parameter
                    attr = ParameterAttributes.In;
                }

                int iArgIndex = i + 1; // +1 because 0 is the return value
                dyn.DefineParameter(iArgIndex, attr, parameter.Name);
            }

            // Generate a dispatcher call
            ILGenerator il = dyn.GetILGenerator();

            // We want to embed the SyncMethod instance into the DynamicMethod. Unsafe code ahead!
            // https://stackoverflow.com/questions/4989681/place-an-object-on-top-of-stack-in-ilgenerator
            GCHandle gcHandle = GCHandle.Alloc(methodAccess);
            IntPtr pMethod = GCHandle.ToIntPtr(gcHandle);

            // Arg0: SyncMethod instance
            if (IntPtr.Size == 4)
            {
                il.Emit(OpCodes.Ldc_I4, pMethod.ToInt32());
            }
            else
            {
                il.Emit(OpCodes.Ldc_I8, pMethod.ToInt64());
            }

            il.Emit(OpCodes.Ldobj, typeof(MethodAccess));

            // Arg1: The instance. For non-static methods this is already added to `parameters`
            //       because of the harmony __instance.
            if (methodAccess.MemberInfo.IsStatic)
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Push all args to stack
            for (int i = 0; i < parameters.Count; ++i)
            {
                il.Emit(OpCodes.Ldarg, i);
            }

            // Request call
            il.EmitCall(
                OpCodes.Call,
                AccessTools.Method(
                    typeof(MethodPatcher),
                    nameof(MethodPatcher.DispatchCallRequest)),
                null);
            il.Emit(OpCodes.Ret);
            return dyn;
        }

        private struct SMethodParameter
        {
            [CanBeNull] public ParameterInfo Info;
            public Type ParameterType;
            public string Name;
        }
    }
}
