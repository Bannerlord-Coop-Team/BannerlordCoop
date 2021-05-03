﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using CoopFramework;
using JetBrains.Annotations;
using Sync.Call;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestConstructorWrapping
    {
        static CoopManaged_TestConstructorWrapping()
        {
            RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(CoopManagedBaz).TypeHandle);
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

        [Fact]
        private void BazIsReleased()
        {
            WeakReference<Baz> reference = null;
            var managedFinalizerCalled = false;
            new Action(() =>
            {
                var baz = new Baz();

                // Set finalizer callback on the managed instance
                var managedBaz = CoopManagedBaz.CreatedInstances[baz];
                CoopManagedBaz.CreatedInstances.Remove(baz);
                managedBaz.OnFinalizerCalled = () => { managedFinalizerCalled = true; };

                reference = new WeakReference<Baz>(baz, false);
                baz = null;
            })();

            // Release the instance
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.False(reference.TryGetTarget(out var restoredBaz));
            Assert.Null(restoredBaz);

            // Check if the managed instance was released as well. Since Baz does not have a destructor, we have to
            // wait for the internal garbage collection of CoopManaged.
            Thread.Sleep(CoopManagedBaz.GCInterval_ms + 50);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.True(managedFinalizerCalled);
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
            public static readonly PatchedInvokable BarSetter = Setter(nameof(Foo.Bar));

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
        }

        private class Bar
        {
            public Action OnFinalizerCalled;

            ~Bar()
            {
                OnFinalizerCalled?.Invoke();
            }
        }

        private class Baz
        {
        }

        private class CoopManagedBaz : CoopManaged<CoopManagedBaz, Baz>
        {
            public static readonly Dictionary<Baz, CoopManagedBaz> CreatedInstances =
                new Dictionary<Baz, CoopManagedBaz>();

            public Action OnFinalizerCalled;

            static CoopManagedBaz()
            {
                AutoWrapAllInstances(instance => new CoopManagedBaz(instance));
            }

            public CoopManagedBaz([NotNull] Baz instance) : base(instance)
            {
                CreatedInstances[instance] = this;
            }

            ~CoopManagedBaz()
            {
                OnFinalizerCalled?.Invoke();
            }
        }
    }
}