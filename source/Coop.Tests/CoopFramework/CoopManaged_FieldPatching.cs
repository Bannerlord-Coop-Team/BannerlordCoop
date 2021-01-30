using CoopFramework;
using JetBrains.Annotations;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_FieldPatching
    {
        class Foo
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

        class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            static CoopManagedFoo()
            {
                When(EActionOrigin.Local)
                    .Changes(Field<int>(nameof(Foo.m_Bar)))
                    .Through(
                        Setter(nameof(Foo.BarProperty)), 
                        Method(nameof(Foo.SetBar)))
                    .Revert();
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
        }

        public CoopManaged_FieldPatching()
        {
            CoopManagedFoo.FieldBuffer.BufferedChanges.Clear();
        }

        [Fact]
        void DoesRevertBarFieldChangeThroughProperty()
        {
            Foo foo = new Foo();
            CoopManagedFoo fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);
            Assert.Empty(CoopManagedFoo.FieldBuffer.BufferedChanges);
            
            // Change through property
            foo.BarProperty = 43;
            Assert.Equal(42, foo.m_Bar);
            Assert.Single(CoopManagedFoo.FieldBuffer.BufferedChanges);
        }
        [Fact]
        void DoesRevertBarFieldChangeThroughMethod()
        {
            Foo foo = new Foo();
            CoopManagedFoo fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);
            Assert.Empty(CoopManagedFoo.FieldBuffer.BufferedChanges);
            
            // Change through method call
            foo.SetBar(43);
            Assert.Equal(42, foo.m_Bar);
            Assert.Single(CoopManagedFoo.FieldBuffer.BufferedChanges);
        }
        [Fact]
        void DoesRevertBarFieldChangeBoth()
        {
            Foo foo = new Foo();
            CoopManagedFoo fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);
            Assert.Empty(CoopManagedFoo.FieldBuffer.BufferedChanges);
            
            // Change through property
            foo.BarProperty = 43;
            Assert.Equal(42, foo.m_Bar);
            Assert.Single(CoopManagedFoo.FieldBuffer.BufferedChanges);
            
            // Change through method call
            foo.SetBar(43);
            Assert.Equal(42, foo.m_Bar);
            Assert.Single(CoopManagedFoo.FieldBuffer.BufferedChanges); // Actually stays single because the buffered change is reused
        }
    }
}