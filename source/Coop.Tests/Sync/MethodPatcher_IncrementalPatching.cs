using System.Linq;
using Sync.Behaviour;
using Sync.Patch;
using Xunit;

namespace Coop.Tests.Sync
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class MethodPatcher_IncrementalPatching
    {
        private static readonly MethodPatch<T0> Patch0 =
            new MethodPatch<T0>(typeof(Foo)).Intercept(nameof(Foo.SyncedMethod));

        private static readonly MethodPatch<T1> Patch1 =
            new MethodPatch<T1>(typeof(Foo)).Intercept(nameof(Foo.SyncedMethod));

        [Fact]
        private void BothPatchesAreApplied()
        {
            var foo = new Foo();
            var bPrefix0Called = false;
            Patch0.Methods.First().Prefix.SetHandler(foo, args =>
            {
                bPrefix0Called = true;
                return ECallPropagation.CallOriginal;
            });


            var bPrefix1Called = false;
            Patch1.Methods.First().Prefix.SetHandler(foo, args =>
            {
                bPrefix1Called = true;
                return ECallPropagation.CallOriginal;
            });

            // Call
            foo.SyncedMethod(42);
            Assert.True(bPrefix0Called);
            Assert.True(bPrefix1Called);
        }

        private class Foo
        {
            public int? LatestArgument;
            public int NumberOfCalls;

            public void SyncedMethod(int iSomeArgument)
            {
                ++NumberOfCalls;
                LatestArgument = iSomeArgument;
            }
        }

        // We just need any type to create a unique MethodPatch static type so the two patches generate differently
        private class T0
        {
        }

        private class T1
        {
        }
    }
}