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
        private static readonly Dictionary<MethodBase, DynamicMethod> Prefixes =
            new Dictionary<MethodBase, DynamicMethod>();

        public static MethodAccess AddPrefix(
            MethodInfo original,
            MethodInfo dispatcher,
            EPatchBehaviour eBehaviour)
        {
            lock (Patcher.HarmonyLock)
            {
                MethodAccess sync = new MethodAccess(original);
                AddPrefix(sync, dispatcher, eBehaviour);
                return sync;
            }
        }

        public static void AddPrefix(
            MethodAccess access,
            MethodInfo dispatcher,
            EPatchBehaviour eBehaviour)
        {
            lock (Patcher.HarmonyLock)
            {
                if (Prefixes.ContainsKey(access.MemberInfo))
                {
                    throw new Exception("Patch already initialized.");
                }

                Prefixes[access.MemberInfo] = GeneratePrefix(access, dispatcher, eBehaviour);

                MethodInfo factoryMethod = typeof(MethodPatchFactory).GetMethod(nameof(GetPrefix));

                HarmonyMethod patch = new HarmonyMethod(factoryMethod)
                {
                    priority = SyncPriority.MethodPatchGeneratedPrefix,
#if DEBUG
                    debug = true
#endif
                };
                Patcher.HarmonyInstance.Patch(access.MemberInfo, patch);
            }
        }

        public static void RemovePrefix(MethodInfo original)
        {
            lock (Patcher.HarmonyLock)
            {
                MethodInfo factoryMethod = typeof(MethodPatchFactory).GetMethod(nameof(GetPrefix));
                Patcher.HarmonyInstance.Unpatch(original, factoryMethod);
                Prefixes.Remove(original);
            }
        }

        public static DynamicMethod GetPrefix(MethodBase original)
        {
            lock (Patcher.HarmonyLock)
            {
                return Prefixes[original];
            }
        }

        /// <summary>
        ///     Generates a <see cref="DynamicMethod" /> to be used as a harmony prefix. The method
        ///     signature exactly matches the original method with an additional and automatically
        ///     captures the instance for non-static functions.
        ///     The generated Prefix captures the <paramref name="method" /> and calls the
        ///     <paramref name="dispatcher" /> with the following arguments:
        ///     `dispatcher(MethodAccess access, object instance, object [] args)`.
        ///     With `args` containing the original method arguments (excluding __instance).
        /// </summary>
        /// <param name="methodAccess">Method that is to be prefixed.</param>
        /// <param name="dispatcher">Dispatcher to be called in the prefix.</param>
        /// <param name="eBehaviour">Return value behaviour of the generated prefix.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static DynamicMethod GeneratePrefix(
            MethodAccess methodAccess,
            MethodInfo dispatcher,
            EPatchBehaviour eBehaviour)
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

            // Arg1: The instance. 
            bool isStatic = methodAccess.MemberInfo.IsStatic;
            if (isStatic)
            {
                il.Emit(OpCodes.Ldnull);
            }
            else
            {
                // Forwarded the injected "__instance" field
                il.Emit(OpCodes.Ldarg_0);

                // Remove the injected instance from the parameters
                parameters.RemoveAt(0);
            }

            // Arg2: object[] of all args. Prepare the array
            LocalBuilder args = il.DeclareLocal(typeof(object[]));

            // start off by creating an object[] with correct size
            il.Emit(OpCodes.Ldc_I4, parameters.Count);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc, args); // store into local var `args`

            // Place argument in array
            for (int i = 0; i < parameters.Count; ++i)
            {
                int iArgIndex = isStatic ? i : i + 1; // +1 because of the injected __instance
                il.Emit(OpCodes.Ldloc, args); // Object reference to `args`
                il.Emit(OpCodes.Ldc_I4, i); // Array index into `args`
                il.Emit(OpCodes.Ldarg, iArgIndex); // value to put at index
                if (parameters[i].ParameterType.IsValueType)
                {
                    il.Emit(OpCodes.Box, parameters[i].ParameterType);
                }

                il.Emit(OpCodes.Stelem_Ref); // pops value, index and array reference from stack.
            }

            // Arg2 done, push it to the stack
            il.Emit(OpCodes.Ldloc, args); // Object reference to `args`

            // Call dispatcher
            il.EmitCall(OpCodes.Call, dispatcher, null);

            switch (eBehaviour)
            {
                case EPatchBehaviour.AlwaysCallOriginal:
                    if (dispatcher.ReturnType != typeof(void))
                    {
                        il.Emit(OpCodes.Pop);
                    }

                    il.Emit(OpCodes.Ldc_I4_1);
                    break;
                case EPatchBehaviour.NeverCallOriginal:
                    if (dispatcher.ReturnType != typeof(void))
                    {
                        il.Emit(OpCodes.Pop);
                    }

                    il.Emit(OpCodes.Ldc_I4_0);
                    break;
                case EPatchBehaviour.CallOriginalBaseOnDispatcherReturn:
                    if (dispatcher.ReturnType != typeof(bool))
                    {
                        throw new Exception(
                            "Invalid dispatcher. Dispatcher function required to return a bool to decided if the original function should be called.");
                    }

                    // Correct value is already on the stack
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eBehaviour), eBehaviour, null);
            }

            il.Emit(OpCodes.Ret);

            return dyn;
        }

        public static void UnpatchAll()
        {
            lock (Patcher.HarmonyLock)
            {
                Patcher.HarmonyInstance.UnpatchAll();
                Prefixes.Clear();
            }
        }

        private struct SMethodParameter
        {
            [CanBeNull] public ParameterInfo Info;
            public Type ParameterType;
            public string Name;
        }
    }
}
