using System;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.Sync
{
    [Collection(
        "UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class MethodPatcher_Prefix
    {
        private class TestRPC
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

        private static readonly MethodPatch Patch = new MethodPatch(typeof(TestRPC))
                                                    .Intercept(nameof(TestRPC.SyncedMethod))
                                                    .Intercept(nameof(TestRPC.StaticSyncedMethod));

        [Fact]
        private void IsSyncHandlerCalled()
        {
            // Register sync handler
            TestRPC instance = new TestRPC();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;

            Assert.True(Patch.TryGetMethod(nameof(TestRPC.SyncedMethod), out MethodAccess method));
            method.Prefix.SetHandler(instance, args => 
            { 
                ++iNumberOfHandlerCalls;
                return ECallPropagation.Suppress;
            });

            // Trigger the handler
            instance.SyncedMethod(42);
            Assert.Equal(0, instance.NumberOfCalls);
            Assert.Equal(1, iNumberOfHandlerCalls);

            method.Prefix.RemoveHandler(instance);
        }

        [Fact]
        private void OriginalIsCalledIfNoHandlerExists()
        {
            TestRPC instance = new TestRPC();
            Assert.Equal(0, instance.NumberOfCalls);
            instance.SyncedMethod(42);
            Assert.Equal(1, instance.NumberOfCalls);
        }
    }
}
