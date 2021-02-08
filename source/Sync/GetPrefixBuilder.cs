using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Sync
{
    public static class GetPrefixBuilder
    {
        /// <summary>
        ///     Generates a Type with a static method that returns the provided DynamicMethod instance. This method
        ///     can be used as a prefix for Harmony as opposed to the DynamicMethod itself.
        ///
        ///     TODO: At this point there's no real benefit of using DynamicMethod at all. Refactor this.
        /// </summary>
        /// <param name="method"></param>
        /// <typeparam name="TPatchGenerator"></typeparam>
        /// <returns></returns>
        public static MethodInfo CreateFactoryMethod<TPatchGenerator>(DynamicMethod method)
        {
            string sTypeName = typeof(TPatchGenerator).FullName;
            string sMethodName = method.Name;

            if (!UsedTypeNames.ContainsKey(sTypeName))
            {
                UsedTypeNames[sTypeName] = 0;
            }
            else
            {
                UsedTypeNames[sTypeName] += 1;
                sTypeName += UsedTypeNames[sTypeName];
            }
            TypeBuilder typeBuilder = ModuleBuilder.DefineType(sTypeName, TypeAttributes.Class | TypeAttributes.NotPublic);
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(
                sMethodName, 
                MethodAttributes.Static | MethodAttributes.Private, CallingConventions.Standard, 
                typeof(DynamicMethod), 
                new[] { typeof( MethodBase )});

            ILGenerator il = methodBuilder.GetILGenerator();
            
            // Push the DynamicMethod to the stack. Unsafe code ahead!
            // https://stackoverflow.com/questions/4989681/place-an-object-on-top-of-stack-in-ilgenerator
            GCHandle gcHandle = GCHandle.Alloc(method);
            IntPtr pMethod = GCHandle.ToIntPtr(gcHandle);

            if (IntPtr.Size == 4)
            {
                il.Emit(OpCodes.Ldc_I4, pMethod.ToInt32());
            }
            else
            {
                il.Emit(OpCodes.Ldc_I8, pMethod.ToInt64());
            }
            il.Emit(OpCodes.Ldobj, typeof(DynamicMethod));
            
            il.Emit(OpCodes.Ret);
            
            // Build the type
            Type declaringType = typeBuilder.CreateType();
            MethodInfo info = declaringType.GetMethod(sMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            return info;
        }
        
        private static readonly AssemblyBuilder AssemblyBuilder =
            AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(nameof(GetPrefixBuilder)),
                AssemblyBuilderAccess.RunAndCollect);

        private static readonly ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule(
            nameof(GetPrefixBuilder));

        private static readonly Dictionary<string, int> UsedTypeNames = new Dictionary<string, int>();
    }
}