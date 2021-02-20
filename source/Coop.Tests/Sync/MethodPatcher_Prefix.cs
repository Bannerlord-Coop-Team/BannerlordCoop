using System;
using Sync.Behaviour;
using Sync.Patch;
using Xunit;

namespace Coop.Tests.Sync
{
    [Collection(
        "UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class MethodPatcher_Prefix
    {
        private static readonly MethodPatch<MethodPatcher_Prefix> Patch =
            new MethodPatch<MethodPatcher_Prefix>(typeof(TestRPC))
                .Intercept(nameof(TestRPC.SyncedMethod))
                .Intercept(nameof(TestRPC.StaticSyncedMethod));

        [Fact]
        private void IsSyncHandlerCalled()
        {
            // Register sync handler
            var instance = new TestRPC();
            Assert.Equal(0, instance.NumberOfCalls);
            var iNumberOfHandlerCalls = 0;

            Assert.True(Patch.TryGetMethod(nameof(TestRPC.SyncedMethod), out var method));
            method.Prefix.SetHandler(instance, args =>
            {
                ++iNumberOfHandlerCalls;
                return ECallPropagation.Skip;
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
            var instance = new TestRPC();
            Assert.Equal(0, instance.NumberOfCalls);
            instance.SyncedMethod(42);
            Assert.Equal(1, instance.NumberOfCalls);
        }

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
    }
}