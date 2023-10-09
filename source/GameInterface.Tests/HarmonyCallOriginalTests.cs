using Common.Util;
using GameInterface.Services.GameDebug.Patches;
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
        private static AllowedInstance<MyClass> allowedInstance = new AllowedInstance<MyClass>();

        public static bool Prefix(ref MyClass __instance)
        {
            CallStackValidator.Validate(__instance, allowedInstance);

            if (allowedInstance.IsAllowed(__instance)) return true;

            return false;
        }
        
        public static int CallOriginal(MyClass instance)
        {
            using(allowedInstance)
            {
                allowedInstance.Instance = instance;
                int result = instance.MyFn();
                return result;
            }
        }
    }

}
