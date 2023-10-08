using HarmonyLib;
using System.Diagnostics;
using Xunit;

namespace GameInterface.Tests
{
    public class HarmonyCallOriginalTests
    {
        [Fact]
        public void CallOriginal_Full()
        {
            // Arrange
            Harmony harmony = new Harmony(nameof(HarmonyCallOriginalTests));

            var original = AccessTools.Method(typeof(MyClass), nameof(MyClass.MyFn));
            var prefix = AccessTools.Method(typeof(MyPatch), nameof(MyPatch.Prefix));
            var harmonyMethod = new HarmonyMethod(prefix);
            harmony.Patch(original, harmonyMethod);

            // Act
            var myClass = new MyClass();

            // Assert
            Assert.Equal(0, myClass.MyFn());

            Assert.Equal(1, MyPatch.CallOriginal(myClass));

            Assert.Equal(0, TestMod.TestMethod(myClass));
        }
    }

    static class TestMod
    {
        public static int TestMethod(MyClass cls)
        {
            return cls.MyFn();
        }
    }


    public class MyClass
    {
        public int MyFn()
        {
            return 1;
        }
    }

    public class MyPatch
    {
        private static object _lock = new object();
        private static MyClass? _allowedInstance;

        public static bool Prefix(ref MyClass __instance)
        {
            ValidateCallStack(ref __instance, ref _allowedInstance);

            if (__instance == _allowedInstance)
            {
                return true;
            }
            return false;
        }

        public static void ValidateCallStack<TInstance>(ref TInstance instance, ref TInstance allowedInstance) where TInstance : class
        {
            var callstack = new StackTrace();

            foreach (var frame in callstack.GetFrames())
            {
                var method = frame.GetMethod();

                if (method?.DeclaringType?.Namespace?.StartsWith("GameInterface") ?? false)
                {
                    if (instance != allowedInstance)
                    {
                        var patchedMethod = callstack.GetFrame(2);
                        var name = patchedMethod.GetMethod().Name;
                        ;
                        // TODO log not allowed
                    }
                }
            }
        }
        
        public static int CallOriginal(MyClass instance)
        {
            lock (_lock)
            {
                _allowedInstance = instance;
                int result = instance.MyFn();
                _allowedInstance = null;
                return result;
            }
        }
    }

}
