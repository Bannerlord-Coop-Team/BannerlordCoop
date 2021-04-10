using System;
using Sync;
using Sync.Behaviour;
using Sync.Call;
using Sync.Patch;
using Xunit;

namespace Coop.Tests.Sync
{
    public class SyncMethod_Test
    {
        private readonly PatchedInvokable m_StaticSyncedMethod;

        private readonly PatchedInvokable m_SyncedMethod;

        public SyncMethod_Test()
        {
            Assert.True(SomePatch.Patch.TryGetMethod(nameof(A.SyncedMethod), out m_SyncedMethod));
            Assert.True(
                SomePatch.Patch.TryGetMethod(
                    nameof(A.StaticSyncedMethod),
                    out m_StaticSyncedMethod));
        }

        [Fact]
        private void IsRegistered()
        {
            // Statically registered
            Assert.True(Registry.IdToInvokable.ContainsKey(m_SyncedMethod.Id));
            Assert.True(Registry.IdToInvokable.ContainsKey(m_StaticSyncedMethod.Id));
        }

        [Fact]
        private void IsStaticSyncHandlerCalled()
        {
            // Register sync handler
            Assert.Equal(0, A.StaticNumberOfCalls);
            var iNumberOfHandlerCalls = 0;
            m_StaticSyncedMethod.Prefix.SetGlobalHandler(
                (instance, args) =>
                {
                    Assert.Null(instance);
                    ++iNumberOfHandlerCalls;
                    return ECallPropagation.Skip;
                });

            // Trigger the handler
            A.StaticSyncedMethod(42);
            Assert.Equal(0, A.StaticNumberOfCalls);
            Assert.Equal(1, iNumberOfHandlerCalls);
            m_StaticSyncedMethod.Prefix.RemoveGlobalHandler();
        }

        [Fact]
        private void IsSyncHandlerCalled()
        {
            // Register sync handler
            var instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            var iNumberOfHandlerCalls = 0;
            m_SyncedMethod.Prefix.SetHandler(instance, args =>
            {
                ++iNumberOfHandlerCalls;
                return ECallPropagation.Skip;
            });

            // Trigger the handler
            instance.SyncedMethod(42);
            Assert.Equal(0, instance.NumberOfCalls);
            Assert.Equal(1, iNumberOfHandlerCalls);
        }

        [Fact]
        private void OriginalIsCalledIfNoHandlerExists()
        {
            // Trigger the handler
            var instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            instance.SyncedMethod(42);
            Assert.Equal(1, instance.NumberOfCalls);
        }

        [Fact]
        private void OriginalIsCalledOnInvoke()
        {
            // Register sync handler
            var instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            var iNumberOfHandlerCalls = 0;
            m_SyncedMethod.Prefix.SetHandler(instance, args =>
            {
                ++iNumberOfHandlerCalls;
                return ECallPropagation.Skip;
            });

            // Call the original
            var iExpectedValue = 42;
            m_SyncedMethod.Invoke(EOriginator.RemoteAuthority, instance, new object[] {iExpectedValue});
            Assert.Equal(0, iNumberOfHandlerCalls);
            Assert.Equal(1, instance.NumberOfCalls);
            Assert.Equal(iExpectedValue, instance.LatestArgument);
        }

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

        private class SomePatch
        {
            public static readonly MethodPatch<SomePatch> Patch = new MethodPatch<SomePatch>(typeof(A))
                .Intercept(nameof(A.SyncedMethod))
                .Intercept(nameof(A.StaticSyncedMethod));
        }
    }
}