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
    public static class MethodPatchFactory<TPatch>
    {
        private static readonly Dictionary<MethodBase, DynamicMethod> Prefixes =
            new Dictionary<MethodBase, DynamicMethod>();
        private static readonly Dictionary<MethodBase, DynamicMethod> Postfixes =
            new Dictionary<MethodBase, DynamicMethod>();

        private const string GeneratedPrefixName = "Prefix";
        private const string GeneratedPostfixName = "Postfix";

        public static void AddPrefix(
            MethodAccess access,
            MethodInfo dispatcher)
        {
            if (dispatcher.ReturnType != typeof(bool))
            {
                throw new Exception("Prefix dispatcher require a return type of bool that decide if the original function should be called.");
            }
            
            lock (Patcher.HarmonyLock)
            {
                if (Prefixes.ContainsKey(access.MethodBase))
                {
                    throw new Exception("Patch already initialized.");
                }

                Prefixes[access.MethodBase] = GeneratePatch(GeneratedPrefixName, access, dispatcher);

                MethodInfo factoryMethod = GetPrefixBuilder.CreateFactoryMethod<TPatch>(Prefixes[access.MethodBase]);

                HarmonyMethod patch = new HarmonyMethod(factoryMethod)
                {
                    priority = SyncPriority.MethodPatchGenerated,
#if DEBUG
                    debug = true
#endif
                };
                Patcher.HarmonyInstance.Patch(access.MethodBase, patch);
            }
        }

        public static void AddPostfix(MethodAccess access, MethodInfo dispatcher)
        {
            lock (Patcher.HarmonyLock)
            {
                if (Postfixes.ContainsKey(access.MethodBase))
                {
                    throw new Exception("Patch already initialized.");
                }

                Postfixes[access.MethodBase] = GeneratePatch(GeneratedPostfixName, access, dispatcher);

                MethodInfo factoryMethod = GetPrefixBuilder.CreateFactoryMethod<TPatch>(Postfixes[access.MethodBase]);

                HarmonyMethod patch = new HarmonyMethod(factoryMethod)
                {
                    priority = SyncPriority.MethodPatchGenerated,
#if DEBUG
                    debug = true
#endif
                };
                Patcher.HarmonyInstance.Patch(access.MethodBase, null, patch);
            }
        }
        
        /// <summary>
        ///     Generates a <see cref="DynamicMethod" /> to be used as a harmony prefix. The method
        ///     signature exactly matches the original method that automatically captures the instance
        ///     for non-static functions.
        ///     The generated Prefix captures the <paramref name="method" /> and calls the
        ///     <paramref name="dispatcher" /> with the following arguments:
        ///     `dispatcher(MethodAccess access, object instance, object [] args)`.
        ///     With `args` containing the original method arguments (excluding __instance).
        /// </summary>
        /// <param name="methodAccess">Method that is to be prefixed.</param>
        /// <param name="dispatcher">Dispatcher to be called in the prefix.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static DynamicMethod GeneratePatch(
            string sMethodName,
            MethodAccess methodAccess,
            MethodInfo dispatcher)
        {
            List<SMethodParameter> parameters = methodAccess.MethodBase.GetParameters()
                                                            .Select(
                                                                p => new SMethodParameter
                                                                {
                                                                    Info = p,
                                                                    ParameterType =
                                                                        p.ParameterType,
                                                                    Name = p.Name
                                                                })
                                                            .ToList();
            if (!methodAccess.MethodBase.IsStatic)
            {
                parameters.Insert(
                    0,
                    new SMethodParameter
                    {
                        Info = null,
                        ParameterType = methodAccess.MethodBase.DeclaringType,
                        Name = "__instance"
                    }); // Inject an __instance
            }

            DynamicMethod dyn;
            if (methodAccess.MethodBase.DeclaringType != null)
            {
                // Member method
                dyn= new DynamicMethod(
                    sMethodName,
                    dispatcher.ReturnType,
                    parameters.Select(p => p.ParameterType).ToArray(),
                    methodAccess.MethodBase.DeclaringType,
                    true);
            }
            else
            {
                dyn = new DynamicMethod(
                    sMethodName,
                    dispatcher.ReturnType,
                    parameters.Select(p => p.ParameterType).ToArray());
            }
                

            for (int i = 0; i < parameters.Count; ++i)
            {
                SMethodParameter parameter = parameters[i];
                ParameterAttributes attr = parameter.Info?.Attributes ?? ParameterAttributes.In;

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
            bool isStatic = methodAccess.MethodBase.IsStatic;
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
            il.EmitCall(dispatcher.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, dispatcher, null);

            // Done
            il.Emit(OpCodes.Ret);

            return dyn;
        }

        public static void UnpatchAll()
        {
            lock (Patcher.HarmonyLock)
            {
                Patcher.HarmonyInstance.UnpatchAll();
                Prefixes.Clear();
                Postfixes.Clear();
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
