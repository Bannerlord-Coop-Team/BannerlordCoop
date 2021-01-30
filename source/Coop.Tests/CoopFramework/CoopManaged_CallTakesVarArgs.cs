using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    public class CoopManaged_CallTakesVarArgs
    {
        class Foo
        {
            public int Bar { get; set; } = 42;
            public int Baz { get; set; } = 42;
        }

        class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            static CoopManagedFoo()
            {
                When(ETriggerOrigin.Local)
                    .Calls(
                        Setter(nameof(Foo.Bar)),
                        Setter(nameof(Foo.Baz)))
                    .Suppress();
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
        }

        [Fact]
        void DoesApplyPatches()
        {
            Foo foo = new Foo();
            CoopManagedFoo fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);

            foo.Bar = 43;
            foo.Baz = 43;

            // Unchanged because of the suppress patch
            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);
        }
    }
}