using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestSuppressExecute
    {
        [Fact]
        private void InstanceWithoutSyncCanStillBeChanged()
        {
            var foo = new Foo();
            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);

            // Since the instance is not yet managed by our syncable, we can still call the setters
            foo.Bar = 43;
            Assert.Equal(43, foo.Bar);

            foo.Baz = 43;
            Assert.Equal(43, foo.Baz);
        }

        [Fact]
        private void LocalBarChangeIsSuppressed()
        {
            var foo = new Foo();
            var sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);

            // The configured behaviour ignores local changes to Foo.Bar
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar);
        }

        [Fact]
        private void AuthoritativeBarChangeIsExecute()
        {
            var foo = new Foo();
            var sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);

            CoopManagedFoo.BarSetter.Call(EOriginator.RemoteAuthority, foo, new object[] {43});
            Assert.Equal(43, foo.Bar);
        }

        [Fact]
        private void LocalBazChangeIsExecuted()
        {
            var foo = new Foo();
            var sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Baz);

            // The configured behaviour allows local changes to Foo.Bar
            foo.Baz = 43;
            Assert.Equal(43, foo.Baz);
        }

        [Fact]
        private void AuthoritativeBarChangeIsSuppressed()
        {
            var foo = new Foo();
            var sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Baz);

            CoopManagedFoo.BazSetter.Call(EOriginator.RemoteAuthority, foo, new object[] {43});
            Assert.Equal(42, foo.Baz);
        }

        private class Foo
        {
            public int Bar { get; set; } = 42;
            public int Baz { get; set; } = 42;
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static readonly MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            public static readonly MethodAccess BazSetter = Setter(nameof(Foo.Baz));

            static CoopManagedFoo()
            {
                // Ignore local calls on Foo.Bar
                When(GameLoop)
                    .Calls(BarSetter)
                    .Suppress();

                // Allow local calls on Foo.Baz
                When(GameLoop)
                    .Calls(BazSetter)
                    .Execute();

                // Ignore authoritative calls on Foo.Baz
                When(RemoteAuthority)
                    .Calls(BazSetter)
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