using System;
using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManager_TestDelegate
    {
        [Fact]
        private void FooCallbackIsCalled()
        {
            var foo = new Foo();
            var sync = new CoopManagedFoo(foo);
            var bCallbackWasExecuted = false;
            foo.Callback = call =>
            {
                bCallbackWasExecuted = true;
                return ECallPropagation.CallOriginal;
            };

            foo.Bar = 43;
            Assert.True(bCallbackWasExecuted);
        }

        [Fact]
        private void HandlerCanControlCallPropagation()
        {
            var foo = new Foo();
            var sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);

            // Call original
            var bCallbackWasExecuted = false;
            foo.Callback = call =>
            {
                bCallbackWasExecuted = true;
                return ECallPropagation.CallOriginal;
            };
            foo.Bar = 43;
            Assert.True(bCallbackWasExecuted);
            Assert.Equal(43, foo.Bar);

            // Suppress original call
            bCallbackWasExecuted = false;
            foo.Callback = call =>
            {
                bCallbackWasExecuted = true;
                return ECallPropagation.Skip;
            };
            foo.Bar = 44;
            Assert.True(bCallbackWasExecuted);
            Assert.Equal(43, foo.Bar); // unchanged
        }

        private class Foo
        {
            public Func<IPendingMethodCall, ECallPropagation>
                Callback; // Testing: Will be called by the CoopManagedFoo handling this instance

            public int Bar { get; set; } = 42;
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static readonly MethodAccess BarSetter = Setter(nameof(Foo.Bar));

            static CoopManagedFoo()
            {
                // Ignore local calls on Foo.Bar
                When(GameLoop)
                    .Calls(BarSetter)
                    .DelegateTo(BarSetterHandler);
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }

            public static ECallPropagation BarSetterHandler(IPendingMethodCall pendingMethodCall)
            {
                var fooInstance = pendingMethodCall.Instance as Foo;
                Assert.NotNull(fooInstance);
                Assert.NotNull(fooInstance.Callback);
                return fooInstance.Callback.Invoke(pendingMethodCall);
            }
            
            [SyncFactory]
            private static SynchronizationClient GetSynchronization()
            {
                return new Mock<SynchronizationClient>().Object;
            }
        }
    }
}