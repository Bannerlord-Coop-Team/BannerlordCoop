using CoopFramework;
using JetBrains.Annotations;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_FieldPatching
    {
        [Fact]
        private void DoesRevertFieldChangeThroughProperty()
        {
            var foo = new Foo();
            var fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);

            // Change through property
            foo.BarProperty = 43;
            Assert.Equal(42, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);

            foo.BazProperty = 43;
            Assert.Equal(42, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);
        }

        [Fact]
        private void DoesRevertFieldChangeThroughMethod()
        {
            var foo = new Foo();
            var fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);

            // Change through method call
            foo.SetBoth(43);
            Assert.Equal(42, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);
        }

        [Fact]
        private void DoesRevertFieldChangeBoth()
        {
            var foo = new Foo();
            var fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);

            // Change through property
            foo.BarProperty = 43;
            foo.BazProperty = 43;
            Assert.Equal(42, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);

            // Change through method call
            foo.SetBoth(43);
            Assert.Equal(42, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);
        }

        private class Foo
        {
            public int m_Bar = 42;
            public int m_Baz = 42;

            public int BarProperty
            {
                set => m_Bar = value;
            }

            public int BazProperty
            {
                set => m_Baz = value;
            }

            public void SetBoth(int i)
            {
                m_Bar = i;
                m_Baz = i;
            }
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            static CoopManagedFoo()
            {
                When(GameLoop)
                    .Changes(
                        Field<int>(nameof(Foo.m_Bar)),
                        Field<int>(nameof(Foo.m_Baz)))
                    .Through(
                        Setter(nameof(Foo.BarProperty)),
                        Setter(nameof(Foo.BazProperty)),
                        Method(nameof(Foo.SetBoth)))
                    .Revert();
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
        }
    }
}