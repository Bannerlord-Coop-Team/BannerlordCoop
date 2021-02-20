using System;
using Sync.Behaviour;
using Sync.Patch;
using Xunit;

namespace Coop.Tests.Sync
{
    public class MethodPatcher_Postfix
    {
        private static readonly MethodPatch<MethodPatcher_Postfix> Patch =
            new MethodPatch<MethodPatcher_Postfix>(typeof(TestRPC))
                .Postfix(nameof(TestRPC.SyncedMethod))
                .Postfix(nameof(TestRPC.StaticSyncedMethod));

        [Fact]
        private void IsHandlerCalled()
        {
            var instance = new TestRPC();
            Assert.Equal(0, instance.NumberOfCalls);
            var iNumberOfHandlerCalls = 0;

            Assert.True(Patch.TryGetMethod(nameof(TestRPC.SyncedMethod), out var method));
            method.Postfix.SetHandler(instance, (eOrigin, args) =>
            {
                Assert.Equal(EOriginator.Game, eOrigin);
                ++iNumberOfHandlerCalls;
            });

            // Trigger the handler
            instance.SyncedMethod(42);
            Assert.Equal(1, instance.NumberOfCalls);
            Assert.Equal(1, iNumberOfHandlerCalls);

            method.Prefix.RemoveHandler(instance);
        }

        [Fact]
        private void IsHandlerCalledAfterCall()
        {
            var instance = new TestRPC();
            Assert.Equal(0, instance.NumberOfCalls);

            Assert.True(Patch.TryGetMethod(nameof(TestRPC.SyncedMethod), out var method));
            var bWasHandlerCalled = false;
            method.Postfix.SetHandler(instance, (eOrigin, args) =>
            {
                bWasHandlerCalled = true;
                Assert.Equal(EOriginator.Game, eOrigin);
                Assert.Single(args);
                Assert.IsType<int>(args[0]);
                Assert.True(instance.LatestArgument.HasValue);
                Assert.Equal(instance.LatestArgument.Value, (int) args[0]);
            });

            // Trigger the handler
            Assert.False(instance.LatestArgument.HasValue);
            instance.SyncedMethod(42);
            Assert.True(bWasHandlerCalled);
            Assert.True(instance.LatestArgument.HasValue);

            method.Prefix.RemoveHandler(instance);
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