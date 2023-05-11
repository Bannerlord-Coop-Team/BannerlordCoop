using HarmonyLib;
using System;
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
            harmony.Patch(original, new HarmonyMethod(prefix));

            // Act
            var myClass = new MyClass();

            // Assert
            Assert.Equal(0, myClass.MyFn());

            Assert.Equal(1, MyPatch.CallOriginal(myClass));
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
            if(__instance == _allowedInstance)
            {
                return true;
            }
            return false;
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
