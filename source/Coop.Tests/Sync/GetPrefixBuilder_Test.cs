using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Sync;
using Xunit;

namespace Coop.Tests.Sync
{
    public class GetPrefixBuilder_Test
    {
        private delegate int PlusOne(int i);
        private static DynamicMethod CreateDynamicMethod()
        {
            DynamicMethod method = new DynamicMethod("PlusOne",
                typeof(int), new[] {typeof(int)});

            ILGenerator il = method.GetILGenerator(256);
            
            il.Emit(OpCodes.Ldarg_0); // Push arg0 to stack
            il.Emit(OpCodes.Ldc_I4_1); // Push 1 to stack
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ret);
            return method;
        }
        
        [Fact]
        void CreateDynamicMethodWorks()
        {
            DynamicMethod method = CreateDynamicMethod();
            PlusOne addOne = (PlusOne) method.CreateDelegate(typeof(PlusOne));
            Assert.Equal(1, addOne(0));
            Assert.Equal(2, addOne(1));
            Assert.Equal(43, addOne(42));
        }

        [Fact]
        void BuilderReturnsCorrectMethodInfo()
        {
            DynamicMethod method = CreateDynamicMethod();
            MethodInfo info = GetPrefixBuilder.CreateFactoryMethod<GetPrefixBuilder_Test>(method);
            Assert.NotNull(info);
            Assert.Equal(typeof(DynamicMethod), info.ReturnType);
            Assert.Equal(new Type[]{ typeof(MethodBase) }, info.GetParameters().Select(p => p.ParameterType).ToArray());
        }
        
        [Fact]
        void ReturnsCorrectDynamicMethod()
        {
            DynamicMethod method = CreateDynamicMethod();
            MethodInfo info = GetPrefixBuilder.CreateFactoryMethod<GetPrefixBuilder_Test>(method);
            object retVal = info.Invoke(null, new object[] { null });
            Assert.NotNull(retVal);
            Assert.IsType<DynamicMethod>(retVal);
            Assert.Same((DynamicMethod) retVal, method);
        }
    }
}