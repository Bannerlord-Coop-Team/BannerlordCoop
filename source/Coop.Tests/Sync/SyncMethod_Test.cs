using HarmonyLib;
using Sync;
using Sync.Attributes;
using Xunit;

namespace Coop.Tests.Sync
{
    public class SyncMethod_Test
    {
        private class A
        {
            public int? LatestArgument;
            public int NumberOfCalls;

            public void SyncedMethod(int iSomeArgument)
            {
                ++NumberOfCalls;
                LatestArgument = iSomeArgument;
            }
        }

        [Patch]
        private class SomePatch
        {
            public static readonly SyncMethod MethodSynchronization =
                new SyncMethod(AccessTools.Method(typeof(A), nameof(A.SyncedMethod)));

            [SyncCall(typeof(A), nameof(A.SyncedMethod))]
            public static void CustomPrefix(A __instance, int iSomeArgument)
            {
                MethodSynchronization.RequestCall(__instance, new object[] {iSomeArgument});
            }
        }

        private static bool m_bHasPatched;
        private readonly Harmony m_Harmony = new Harmony("Coop.Test");

        private void ApplyPatch()
        {
            if (!m_bHasPatched)
            {
                m_bHasPatched = true;
                Patcher.ApplyPatch(m_Harmony, typeof(SomePatch));
            }
        }

        [Fact]
        private void IsRegistered()
        {
            // Statically registered
            Assert.True(MethodRegistry.MethodToId.ContainsKey(SomePatch.MethodSynchronization));
        }

        [Fact]
        private void IsSyncHandlerCalled()
        {
            ApplyPatch();

            // Register sync handler
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;
            SomePatch.MethodSynchronization.SetSyncHandler(
                instance,
                args => { ++iNumberOfHandlerCalls; });

            // Trigger the handler
            instance.SyncedMethod(42);
            Assert.Equal(0, instance.NumberOfCalls);
            Assert.Equal(1, iNumberOfHandlerCalls);
        }

        [Fact]
        private void OriginalIsCalledOnInvoke()
        {
            ApplyPatch();

            // Register sync handler
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;
            SomePatch.MethodSynchronization.SetSyncHandler(
                instance,
                args => { ++iNumberOfHandlerCalls; });

            // Call the original
            int iExpectedValue = 42;
            SomePatch.MethodSynchronization.CallOriginal(instance, new object[] {iExpectedValue});
            Assert.Equal(0, iNumberOfHandlerCalls);
            Assert.Equal(1, instance.NumberOfCalls);
            Assert.Equal(iExpectedValue, instance.LatestArgument);
        }
    }
}
