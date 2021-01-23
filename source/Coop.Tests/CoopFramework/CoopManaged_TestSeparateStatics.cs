using System;
using CoopFramework;
using JetBrains.Annotations;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestSeparateStatics
    {
        class Foo
        {
            // For some simpler types the runtime apparently does not generate a destructor. But we need one for the automatic lifetime management.
            ~Foo()
            {
            }
            public int Bar { get; set; } = 42;
        }
        
        class Foo2
        {
            // For some simpler types the runtime apparently does not generate a destructor. But we need one for the automatic lifetime management.
            ~Foo2()
            {
            }
            public int Bar { get; set; } = 42;
        }
        
        class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            static CoopManagedFoo()
            {
                When(ETriggerOrigin.Local)
                    .Calls(BarSetter)
                    .Suppress();
                EnabledForAllInstances((instance => new CoopManagedFoo(null, instance)));
            }

            public CoopManagedFoo(ISynchronization sync, [NotNull] Foo instance) : base(sync, instance)
            {
            }
        }
        
        class CoopManagedFoo2 : CoopManaged<CoopManagedFoo2, Foo2>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo2.Bar));
            static CoopManagedFoo2()
            {
                When(ETriggerOrigin.Local)
                    .Calls(BarSetter)
                    .Suppress();
                EnabledForAllInstances((instance => new CoopManagedFoo2(null, instance)));
            }

            public CoopManagedFoo2(ISynchronization sync, [NotNull] Foo2 instance) : base(sync, instance)
            {
            }
        }
        
        static CoopManaged_TestSeparateStatics()
        {
            // Ensure the static constructor is called
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo).TypeHandle);  
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo2).TypeHandle);  
        }

        public CoopManaged_TestSeparateStatics()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Fact]
        void FooIsAutoPatched()
        {
            Assert.Empty(CoopManagedFoo.ManagedInstances);
            Foo foo = new Foo();
            Assert.Single(CoopManagedFoo.ManagedInstances);
            Assert.Equal(42, foo.Bar);
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar); // Suppressed
        }
        
        [Fact]
        void Foo2IsAutoPatched()
        {
            Assert.Empty(CoopManagedFoo2.ManagedInstances);
            Foo2 foo = new Foo2();
            Assert.Single(CoopManagedFoo2.ManagedInstances);
            Assert.Equal(42, foo.Bar);
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar); // Suppressed
        }
        
        [Fact]
        void ManagedInstancePoolsAreSeparate()
        {
            Assert.Empty(CoopManagedFoo.ManagedInstances);
            Assert.Empty(CoopManagedFoo2.ManagedInstances);
            Foo foo = new Foo();
            Assert.Single(CoopManagedFoo.ManagedInstances);
            Assert.Empty(CoopManagedFoo2.ManagedInstances);
            Foo2 foo2 = new Foo2();
            Assert.Single(CoopManagedFoo.ManagedInstances);
            Assert.Single(CoopManagedFoo2.ManagedInstances);
        }
    }
}