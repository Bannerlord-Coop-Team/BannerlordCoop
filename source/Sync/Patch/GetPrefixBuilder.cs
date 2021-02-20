using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Sync.Patch
{
    public static class GetPrefixBuilder
    {
        private static readonly AssemblyBuilder AssemblyBuilder =
            AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(nameof(GetPrefixBuilder)),
                AssemblyBuilderAccess.RunAndCollect);

        private static readonly ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule(
            nameof(GetPrefixBuilder));

        private static readonly Dictionary<string, int> UsedTypeNames = new Dictionary<string, int>();

        /// <summary>
        ///     Generates a Type with a static method that returns the provided DynamicMethod instance. This method
        ///     can be used as a prefix for Harmony as opposed to the DynamicMethod itself.
        ///     TODO: At this point there's no real benefit of using DynamicMethod at all. Refactor this.
        /// </summary>
        /// <param name="method"></param>
        /// <typeparam name="TPatchGenerator"></typeparam>
        /// <returns></returns>
        public static MethodInfo CreateFactoryMethod<TPatchGenerator>(DynamicMethod method)
        {
            var sTypeName = typeof(TPatchGenerator).FullName;
            var sMethodName = method.Name;

            if (!UsedTypeNames.ContainsKey(sTypeName))
            {
                UsedTypeNames[sTypeName] = 0;
            }
            else
            {
                UsedTypeNames[sTypeName] += 1;
                sTypeName += "_" + UsedTypeNames[sTypeName];
            }

            var typeBuilder = ModuleBuilder.DefineType(sTypeName, TypeAttributes.Class | TypeAttributes.NotPublic);
            var methodBuilder = typeBuilder.DefineMethod(
                sMethodName,
                MethodAttributes.Static | MethodAttributes.Private, CallingConventions.Standard,
                typeof(DynamicMethod),
                new[] {typeof(MethodBase)});

            var il = methodBuilder.GetILGenerator();

            // Push the DynamicMethod to the stack. Unsafe code ahead!
            // https://stackoverflow.com/questions/4989681/place-an-object-on-top-of-stack-in-ilgenerator
            var gcHandle = GCHandle.Alloc(method);
            var pMethod = GCHandle.ToIntPtr(gcHandle);

            if (IntPtr.Size == 4)
                il.Emit(OpCodes.Ldc_I4, pMethod.ToInt32());
            else
                il.Emit(OpCodes.Ldc_I8, pMethod.ToInt64());
            il.Emit(OpCodes.Ldobj, typeof(DynamicMethod));

            il.Emit(OpCodes.Ret);

            // Build the type
            var declaringType = typeBuilder.CreateType();
            var info = declaringType.GetMethod(sMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            return info;
        }
    }
}