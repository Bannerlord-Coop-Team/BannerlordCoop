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
    public class CoopManaged_Test0
    {
        class Foo
        {
            public int Bar { get; set; } = 42;
        }

        class CoopManagedFoo : CoopManaged<Foo>
        {
            private static readonly PropertyPatch BarPatch =
                new PropertyPatch(typeof(Foo))
                    .InterceptSetter(nameof(Foo.Bar));

            static CoopManagedFoo()
            {
                // Local calls are ignored
                When(ETriggerOrigin.Local)
                    .Calls(BarPatch.Setters)
                    .Ignore();
            }

            [PatchInitializer]
            public static void Init()
            {
                InitPatches(typeof(CoopManagedFoo));
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
        }

        static CoopManaged_Test0()
        {
            // Invoke the initializer manually
            MethodInfo initializer = typeof(CoopManagedFoo)
                .GetMethods()
                .Single(m => m.IsDefined(typeof(PatchInitializerAttribute)));
            initializer.Invoke(null, null);
        }

        [Fact]
        void TestNonSyncableInstanceCanStillBeCalled()
        {
            Foo foo = new Foo();
            Assert.Equal(42, foo.Bar);

            // Since the instance is not yet managed by our syncable, we can still call the setter
            foo.Bar = 43;
            Assert.Equal(43, foo.Bar);
        }

        [Fact]
        void TestLocalSetterCallIsIgnored()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(foo);
            Assert.Equal(42, foo.Bar);

            // The setter should be ignore as defined by SyncableFoo
            foo.Bar = 43;
            Assert.Equal(42, foo.Bar);
        }
    }
}
