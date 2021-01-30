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

        [Fact]
        void DoesRevertBarFieldChange()
        {
            Foo foo = new Foo();
            CoopManagedFoo fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.m_Bar);
            
            // Change through property
            foo.BarProperty = 43;

            // Unchanged because of the suppress patch
            Assert.Equal(42, foo.m_Bar);
        }
    }
}