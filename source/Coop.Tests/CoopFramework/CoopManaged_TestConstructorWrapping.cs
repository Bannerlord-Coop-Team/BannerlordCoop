using System;
using CoopFramework;
using JetBrains.Annotations;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestConstructorWrapping
    {
        class Foo
        {
            ~Foo()
            {
                OnFinalizerCalled?.Invoke();
            }
            public int Bar { get; set; } = 42;
            public Action OnFinalizerCalled;
        }

        class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            static CoopManagedFoo()
            {
                // Broadcast local calls on Foo.Bar instead of applying it directly
                When(EActionOrigin.Local)
                    .Calls(BarSetter)
                    .Suppress();
                
                AutoWrapAllInstances((instance => new CoopManagedFoo(instance)));
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
        }

        static CoopManaged_TestConstructorWrapping()
        {
            // Ensure the static constructor is called
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo).TypeHandle);  
        }

        [Fact]
        void FooIsAutomaticallyPatched()
        {
            Foo foo = new Foo();
            Assert.Equal(42, foo.Bar);
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar); // Suppressed
        }
        
        class Bar
        {
            ~Bar()
            {
                OnFinalizerCalled?.Invoke();
            }
            public Action OnFinalizerCalled;
        }
        
        [Fact]
        void BarIsReleased()
        {
            // Bar is not patched at all, this this just verifies that the method of tracking the finalizer call actually works.
            WeakReference<Bar> reference = null;
            bool finalizerCalled = false;
            new Action(() => 
            {
                var bar = new Bar();
                bar.OnFinalizerCalled = () =>
                {
                    finalizerCalled = true;
                };
                reference = new WeakReference<Bar>(bar, false);
                bar = null;
            })();
            
            // Release the instance
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.False(reference.TryGetTarget(out Bar restoredInstance));
            Assert.Null(restoredInstance);
            Assert.True(finalizerCalled);
        }
        
        [Fact]
        void FooIsReleased()
        {
            WeakReference<Foo> reference = null;
            bool finalizerCalled = false;
            new Action(() => 
            {
                var foo = new Foo();
                foo.OnFinalizerCalled = () =>
                {
                    finalizerCalled = true;
                };
                reference = new WeakReference<Foo>(foo, false);
                foo = null;
            })();
            
            // Release the instance
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.False(reference.TryGetTarget(out Foo restoredFoo));
            Assert.Null(restoredFoo);
            Assert.True(finalizerCalled);
        }
    }
}