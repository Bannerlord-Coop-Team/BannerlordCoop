using System;
using System.Runtime.CompilerServices;
using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestSeparateStatics
    {
        static CoopManaged_TestSeparateStatics()
        {
            // Ensure the static constructor is called
            RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo2).TypeHandle);
        }

        public CoopManaged_TestSeparateStatics()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Fact]
        private void FooIsAutoPatched()
        {
            Assert.Empty(CoopManagedFoo.AutoWrappedInstances);
            var foo = new Foo();
            Assert.Single(CoopManagedFoo.AutoWrappedInstances);
            Assert.Equal(42, foo.Bar);
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar); // Suppressed
        }

        [Fact]
        private void Foo2IsAutoPatched()
        {
            Assert.Empty(CoopManagedFoo2.AutoWrappedInstances);
            var foo = new Foo2();
            Assert.Single(CoopManagedFoo2.AutoWrappedInstances);
            Assert.Equal(42, foo.Bar);
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar); // Suppressed
        }

        [Fact]
        private void ManagedInstancePoolsAreSeparate()
        {
            Assert.Empty(CoopManagedFoo.AutoWrappedInstances);
            Assert.Empty(CoopManagedFoo2.AutoWrappedInstances);
            var foo = new Foo();
            Assert.Single(CoopManagedFoo.AutoWrappedInstances);
            Assert.Empty(CoopManagedFoo2.AutoWrappedInstances);
            var foo2 = new Foo2();
            Assert.Single(CoopManagedFoo.AutoWrappedInstances);
            Assert.Single(CoopManagedFoo2.AutoWrappedInstances);
        }

        private class Foo
        {
            public int Bar { get; set; } = 42;

            // For some simpler types the runtime apparently does not generate a destructor. But we need one for the automatic lifetime management.
            ~Foo()
            {
            }
        }

        private class Foo2
        {
            public int Bar { get; set; } = 42;

            // For some simpler types the runtime apparently does not generate a destructor. But we need one for the automatic lifetime management.
            ~Foo2()
            {
            }
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static readonly MethodAccess BarSetter = Setter(nameof(Foo.Bar));

            static CoopManagedFoo()
            {
                When(EOriginator.Game)
                    .Calls(BarSetter)
                    .Suppress();
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

        private class CoopManagedFoo2 : CoopManaged<CoopManagedFoo2, Foo2>
        {
            public static readonly MethodAccess BarSetter = Setter(nameof(Foo2.Bar));

            static CoopManagedFoo2()
            {
                When(EOriginator.Game)
                    .Calls(BarSetter)
                    .Suppress();
                AutoWrapAllInstances(instance => new CoopManagedFoo2(instance));
            }

            public CoopManagedFoo2([NotNull] Foo2 instance) : base(instance)
            {
            }
            [SyncFactory]
            private static ISynchronization GetSynchronization()
            {
                return new Mock<ISynchronization>().Object;
            }
            
        }
    }
}