using System.Linq;
using Sync;
using Xunit;

namespace Coop.Tests.Sync
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class MethodAccess_Test
    {
        class Foo
        {
            public bool WasTouchCalled = false;
            public int Arg = 0;

            public void Touch(int iArg)
            {
                WasTouchCalled = true;
                Arg = iArg;
            }
        }
        
        private static readonly MethodPatch Patch = new MethodPatch(typeof(Foo))
            .Intercept(nameof(Foo.Touch));

        [Fact]
        void CanChangeArgPrimitive()
        {
            Foo foo = new Foo();
            
            Patch.Methods.First().SetHandler(foo, (args) =>
            {
                Assert.Single(args);
                ref object arg0 = ref args[0];
                Assert.IsType<int>(arg0);
                return true;
            });
            foo.Touch(1);
        }
    }
}