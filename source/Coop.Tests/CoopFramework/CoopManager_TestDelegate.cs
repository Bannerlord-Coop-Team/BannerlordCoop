using System;
using System.Linq;
using System.Reflection;
using CoopFramework;
using JetBrains.Annotations;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManager_TestDelegate
    {
        class Foo
        {
            public int Bar { get; set; } = 42;

            public Func<IPendingMethodCall, ECallPropagation> Callback; // Testing: Will be called by the CoopManagedFoo handling this instance
        }

        class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            static CoopManagedFoo()
            {
                // Ignore local calls on Foo.Bar
                When(ETriggerOrigin.Local)
                    .Calls(BarSetter)
                    .DelegateTo(BarSetterHandler);
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }

            public static ECallPropagation BarSetterHandler(IPendingMethodCall pendingMethodCall)
            {
                Foo fooInstance = pendingMethodCall.Instance as Foo;
                Assert.NotNull(fooInstance);
                Assert.NotNull(fooInstance.Callback);
                return fooInstance.Callback.Invoke(pendingMethodCall);
            }

            protected override ISynchronization GetSynchronization()
            {
                return null;
            }
        }

        [Fact]
        void FooCallbackIsCalled()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(foo);
            bool bCallbackWasExecuted = false;
            foo.Callback = call =>
            {
                bCallbackWasExecuted = true;
                return ECallPropagation.CallOriginal;
            };
            
            foo.Bar = 43;
            Assert.True(bCallbackWasExecuted);
        }

        [Fact]
        void HandlerCanControlCallPropagation()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);
            
            // Call original
            bool bCallbackWasExecuted = false;
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
                return ECallPropagation.Suppress;
            };
            foo.Bar = 44;
            Assert.True(bCallbackWasExecuted);
            Assert.Equal(43, foo.Bar); // unchanged
        }
    }
}