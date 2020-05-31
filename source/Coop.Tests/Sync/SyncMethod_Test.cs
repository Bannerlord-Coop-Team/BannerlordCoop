using System;
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
            [ThreadStatic] public static int? StaticLatestArgument;
            [ThreadStatic] public static int StaticNumberOfCalls;
            public int? LatestArgument;
            public int NumberOfCalls;

            public void SyncedMethod(int iSomeArgument)
            {
                ++NumberOfCalls;
                LatestArgument = iSomeArgument;
            }

            public static void StaticSyncedMethod(int iSomeArgument)
            {
                ++StaticNumberOfCalls;
                StaticLatestArgument = iSomeArgument;
            }
        }

        [Patch]
        private class SomePatch
        {
            public static readonly SyncMethod MethodSynchronization =
                new SyncMethod(AccessTools.Method(typeof(A), nameof(A.SyncedMethod)));

            public static readonly SyncMethod StaticMethodSynchronization =
                new SyncMethod(AccessTools.Method(typeof(A), nameof(A.StaticSyncedMethod)));

            [SyncCall(typeof(A), nameof(A.SyncedMethod))]
            public static bool CustomPrefix(A __instance, int iSomeArgument)
            {
                return MethodSynchronization.RequestCall(__instance, iSomeArgument);
            }

            [SyncCall(typeof(A), nameof(A.StaticSyncedMethod))]
            public static bool CustomPrefixStatic(int iSomeArgument)
            {
                return StaticMethodSynchronization.RequestCall(null, iSomeArgument);
            }
        }

        private static bool m_bHasPatched;

        private void ApplyPatch()
        {
            if (!m_bHasPatched)
            {
                m_bHasPatched = true;
                Patcher.ApplyPatch(typeof(SomePatch));
            }
        }

        [Fact]
        private void IsRegistered()
        {
            ApplyPatch();

            // Statically registered
            Assert.True(MethodRegistry.MethodToId.ContainsKey(SomePatch.MethodSynchronization));
            Assert.True(
                MethodRegistry.MethodToId.ContainsKey(SomePatch.StaticMethodSynchronization));
        }

        [Fact]
        private void IsStaticSyncHandlerCalled()
        {
            ApplyPatch();

            // Register sync handler
            Assert.Equal(0, A.StaticNumberOfCalls);
            int iNumberOfHandlerCalls = 0;
            SomePatch.StaticMethodSynchronization.SetGlobalHandler(
                (instance, args) =>
                {
                    Assert.Null(instance);
                    ++iNumberOfHandlerCalls;
                });

            // Trigger the handler
            A.StaticSyncedMethod(42);
            Assert.Equal(0, A.StaticNumberOfCalls);
            Assert.Equal(1, iNumberOfHandlerCalls);
            SomePatch.StaticMethodSynchronization.RemoveGlobalHandler();
        }

        [Fact]
        private void IsSyncHandlerCalled()
        {
            ApplyPatch();

            // Register sync handler
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;
            SomePatch.MethodSynchronization.SetInstanceHandler(
                instance,
                args => { ++iNumberOfHandlerCalls; });

            // Trigger the handler
            instance.SyncedMethod(42);
            Assert.Equal(0, instance.NumberOfCalls);
            Assert.Equal(1, iNumberOfHandlerCalls);
        }

        [Fact]
        private void OriginalIsCalledIfNoHandlerExists()
        {
            ApplyPatch();

            // Trigger the handler
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            instance.SyncedMethod(42);
            Assert.Equal(1, instance.NumberOfCalls);
        }

        [Fact]
        private void OriginalIsCalledOnInvoke()
        {
            ApplyPatch();

            // Register sync handler
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;
            SomePatch.MethodSynchronization.SetInstanceHandler(
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
