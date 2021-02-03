using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_CallTakesVarArgs
    {
        [Fact]
        private void DoesApplyPatches()
        {
            var foo = new Foo();
            var fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);

            foo.Bar = 43;
            foo.Baz = 43;

            // Unchanged because of the suppress patch
            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);
        }

        private class Foo
        {
            public int Bar { get; set; } = 42;
            public int Baz { get; set; } = 42;
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            static CoopManagedFoo()
            {
                When(GameLoop)
                    .Calls(
                        Setter(nameof(Foo.Bar)),
                        Setter(nameof(Foo.Baz)))
                    .Suppress();
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
    }
}