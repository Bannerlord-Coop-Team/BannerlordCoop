using GameInterface.Services.MobilePartyAIs.Patches;
using HarmonyLib;
using System.Diagnostics;
using System.Threading;
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
