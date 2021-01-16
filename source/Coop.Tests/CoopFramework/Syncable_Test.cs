using CoopFramework;
using JetBrains.Annotations;
using Sync;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class Syncable_Test
    {
        class Foo
        {
            public int Bar { get; set; }
        }

        class SyncableFoo : Syncable<Foo>
        {
            // Patch Foo.Bar setter
            private static readonly PropertyPatch Patch =
                new PropertyPatch(typeof(Foo))
                    .InterceptSetter(nameof(Foo.Bar));

            static SyncableFoo()
            {
                // Statically configure the behaviour of our patch
                When(ECaller.Local)
                    .Calls(Patch.Setters)
                    .DelegateTo(LocalBarChangedHandler);
            }

            static void LocalBarChangedHandler(object instance, IPendingMethodCall call)
            {
                SyncableFoo self = instance as SyncableFoo;
            }
            
            public SyncableFoo([NotNull] Foo instance) : base(instance)
            {
            }
        }
    }
}
