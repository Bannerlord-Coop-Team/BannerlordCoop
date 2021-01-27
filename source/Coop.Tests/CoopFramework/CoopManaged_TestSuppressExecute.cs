using System.Linq;
using System.Reflection;
using CoopFramework;
using JetBrains.Annotations;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestSuppressExecute
    {
        class Foo
        {
            public int Bar { get; set; } = 42;
            public int Baz { get; set; } = 42;
        }

        class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            public static MethodAccess BazSetter = Setter(nameof(Foo.Baz));
            static CoopManagedFoo()
            {
                // Ignore local calls on Foo.Bar
                When(ETriggerOrigin.Local)
                    .Calls(BarSetter)
                    .Suppress();
                
                // Allow local calls on Foo.Baz
                When(ETriggerOrigin.Local)
                    .Calls(BazSetter)
                    .Execute();
                
                // Ignore authoritative calls on Foo.Baz
                When(ETriggerOrigin.Authoritative)
                    .Calls(BazSetter)
                    .Suppress();
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }

            protected override ISynchronization GetSynchronization()
            {
                return null;
            }
        }

        [Fact]
        void InstanceWithoutSyncCanStillBeChanged()
        {
            Foo foo = new Foo();
            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);

            // Since the instance is not yet managed by our syncable, we can still call the setters
            foo.Bar = 43;
            Assert.Equal(43, foo.Bar);
            
            foo.Baz = 43;
            Assert.Equal(43, foo.Baz);
        }

        [Fact]
        void LocalBarChangeIsSuppressed()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);

            // The configured behaviour ignores local changes to Foo.Bar
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar);
        }

        [Fact]
        void AuthoritativeBarChangeIsExecute()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);

            CoopManagedFoo.BarSetter.Call(ETriggerOrigin.Authoritative, foo, new object[] {43});
            Assert.Equal(43, foo.Bar);
        }
        
        [Fact]
        void LocalBazChangeIsExecuted()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Baz);

            // The configured behaviour allows local changes to Foo.Bar
            foo.Baz = 43;
            Assert.Equal(43, foo.Baz);
        }
        
        [Fact]
        void AuthoritativeBarChangeIsSuppressed()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Baz);

            CoopManagedFoo.BazSetter.Call(ETriggerOrigin.Authoritative, foo, new object[] {43});
            Assert.Equal(42, foo.Baz);
        }
    }
}
