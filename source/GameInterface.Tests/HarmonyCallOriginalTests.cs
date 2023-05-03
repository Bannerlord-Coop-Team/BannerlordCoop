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

            Assert.Equal(1, original.Invoke(myClass, Array.Empty<object>()));
        }
    }


    public class MyClass
    {
        public int MyFn()
        {
            return 1;
        }
    }

    [HarmonyPatch]
    public class MyPatch
    {
        public static bool Prefix()
        {
            return false;
        }
    }

}
