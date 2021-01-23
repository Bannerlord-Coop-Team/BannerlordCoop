using System;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.Sync
{
    public class SyncMethod_Test
    {
        public SyncMethod_Test()
        {
            Assert.True(SomePatch.Patch.TryGetMethod(nameof(A.SyncedMethod), out m_SyncedMethod));
            Assert.True(
                SomePatch.Patch.TryGetMethod(
                    nameof(A.StaticSyncedMethod),
                    out m_StaticSyncedMethod));
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
            public static readonly MethodPatch Patch = new MethodPatch(typeof(A))
                                                       .Intercept(nameof(A.SyncedMethod))
                                                       .Intercept(nameof(A.StaticSyncedMethod));
        }

        private readonly MethodAccess m_SyncedMethod;
        private readonly MethodAccess m_StaticSyncedMethod;

        [Fact]
        private void IsRegistered()
        {
            // Statically registered
            Assert.True(MethodRegistry.MethodToId.ContainsKey(m_SyncedMethod));
            Assert.True(MethodRegistry.MethodToId.ContainsKey(m_StaticSyncedMethod));
        }

        [Fact]
        private void IsStaticSyncHandlerCalled()
        {
            // Register sync handler
            Assert.Equal(0, A.StaticNumberOfCalls);
            int iNumberOfHandlerCalls = 0;
            m_StaticSyncedMethod.Prefix.SetGlobalHandler(
                (instance, args) =>
                {
                    Assert.Null(instance);
                    ++iNumberOfHandlerCalls;
                    return ECallPropagation.Suppress;
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
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;
            m_SyncedMethod.Prefix.SetHandler(instance, args =>
            {
                ++iNumberOfHandlerCalls;
                return ECallPropagation.Suppress;
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
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            instance.SyncedMethod(42);
            Assert.Equal(1, instance.NumberOfCalls);
        }

        [Fact]
        private void OriginalIsCalledOnInvoke()
        {
            // Register sync handler
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;
            m_SyncedMethod.Prefix.SetHandler(instance, args =>
            {
                ++iNumberOfHandlerCalls;
                return ECallPropagation.Suppress;
            });

            // Call the original
            int iExpectedValue = 42;
            m_SyncedMethod.Call(ETriggerOrigin.Authoritative, instance, new object[] {iExpectedValue});
            Assert.Equal(0, iNumberOfHandlerCalls);
            Assert.Equal(1, instance.NumberOfCalls);
            Assert.Equal(iExpectedValue, instance.LatestArgument);
        }
    }
}
