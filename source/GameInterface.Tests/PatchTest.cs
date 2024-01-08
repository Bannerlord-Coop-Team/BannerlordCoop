using HarmonyLib;
using Xunit;

namespace GameInterface.Tests
{
    public class PatchTest
    {
        [Fact]
        public void HarmonyPatchesAll()
        {
            var harmony = new Harmony("Test");

            harmony.PatchAll(typeof(GameInterface).Assembly);
        }
    }
}
