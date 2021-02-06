using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_FieldPatching
    {
        [Fact]
        private void DoesRevertBarFieldChangeThroughProperty()
        {
            var foo = new Foo();
            var fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);

            // Change through property
            foo.BarProperty = 43;
            Assert.Equal(42, foo.m_Bar);
        }

        [Fact]
        private void DoesRevertBarFieldChangeThroughMethod()
        {
            var foo = new Foo();
            var fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);

            // Change through method call
            foo.SetBar(43);
            Assert.Equal(42, foo.m_Bar);
        }

        [Fact]
        private void DoesRevertBarFieldChangeBoth()
        {
            var foo = new Foo();
            var fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);

            // Change through property
            foo.BarProperty = 43;
            Assert.Equal(42, foo.m_Bar);

            // Change through method call
            foo.SetBar(43);
            Assert.Equal(42, foo.m_Bar);
        }

        private class Foo
        {
            public int m_Bar = 42;

            public int BarProperty
            {
                set => m_Bar = value;
            }

            public void SetBar(int i)
            {
                m_Bar = i;
            }
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            static CoopManagedFoo()
            {
                When(GameLoop)
                    .Changes(Field<int>(nameof(Foo.m_Bar)))
                    .Through(
                        Setter(nameof(Foo.BarProperty)),
                        Method(nameof(Foo.SetBar)))
                    .Revert();
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
            
            [SyncFactory]
            private static SynchronizationClient GetSynchronization()
            {
                return new Mock<SynchronizationClient>().Object;
            }
        }
    }
}