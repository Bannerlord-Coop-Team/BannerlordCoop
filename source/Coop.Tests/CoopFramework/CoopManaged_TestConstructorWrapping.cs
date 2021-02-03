using System;
using System.Runtime.CompilerServices;
using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync;
using Sync.Behaviour;
using Xunit;
using Action = System.Action;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestConstructorWrapping
    {
        static CoopManaged_TestConstructorWrapping()
        {
            Util.CallPatchInitializer(typeof(CoopManagedFoo));
        }

        [Fact]
        private void FooIsAutomaticallyPatched()
        {
            var foo = new Foo();
            Assert.Equal(42, foo.Bar);
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar); // Suppressed
        }

        [Fact]
        private void BarIsReleased()
        {
            // Bar is not patched at all, this this just verifies that the method of tracking the finalizer call actually works.
            WeakReference<Bar> reference = null;
            var finalizerCalled = false;
            new Action(() =>
            {
                var bar = new Bar();
                bar.OnFinalizerCalled = () => { finalizerCalled = true; };
                reference = new WeakReference<Bar>(bar, false);
                bar = null;
            })();

            // Release the instance
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.False(reference.TryGetTarget(out var restoredInstance));
            Assert.Null(restoredInstance);
            Assert.True(finalizerCalled);
        }

        [Fact]
        private void FooIsReleased()
        {
            WeakReference<Foo> reference = null;
            var finalizerCalled = false;
            new Action(() =>
            {
                var foo = new Foo();
                foo.OnFinalizerCalled = () => { finalizerCalled = true; };
                reference = new WeakReference<Foo>(foo, false);
                foo = null;
            })();

            // Release the instance
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.False(reference.TryGetTarget(out var restoredFoo));
            Assert.Null(restoredFoo);
            Assert.True(finalizerCalled);
        }

        private class Foo
        {
            public Action OnFinalizerCalled;
            public int Bar { get; set; } = 42;

            ~Foo()
            {
                OnFinalizerCalled?.Invoke();
            }
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static readonly MethodAccess BarSetter = Setter(nameof(Foo.Bar));

            static CoopManagedFoo()
            {
                // Broadcast local calls on Foo.Bar instead of applying it directly
                When(GameLoop)
                    .Calls(BarSetter)
                    .Skip();

                AutoWrapAllInstances(instance => new CoopManagedFoo(instance));
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
            [SyncFactory]
            private static ISynchronization GetSynchronization()
            {
                return new Mock<ISynchronization>().Object;
            }
            
        }

        private class Bar
        {
            public Action OnFinalizerCalled;

            ~Bar()
            {
                OnFinalizerCalled?.Invoke();
            }
        }
    }
}