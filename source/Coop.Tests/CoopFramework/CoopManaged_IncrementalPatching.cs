using CoopFramework;
using JetBrains.Annotations;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_IncrementalPatching
    {
        class Foo
        {
            // For some simpler types the runtime apparently does not generate a destructor. But we need one for the automatic lifetime management.
            ~Foo()
            {
            }
            public int Bar { get; set; } = 42;
        }
        
        class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            static CoopManagedFoo()
            {
                When(ETriggerOrigin.Local)
                    .Calls(BarSetter)
                    .Execute();
            }

            public CoopManagedFoo(ISynchronization sync, [NotNull] Foo instance) : base(sync, instance)
            {
            }
        }
        
        class CoopManagedFoo2 : CoopManaged<CoopManagedFoo2, Foo>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            static CoopManagedFoo2()
            {
                When(ETriggerOrigin.Local)
                    .Calls(BarSetter)
                    .Execute();
            }

            public CoopManagedFoo2(ISynchronization sync, [NotNull] Foo instance) : base(sync, instance)
            {
            }
        }

        static CoopManaged_IncrementalPatching()
        {
            // Initialize the static constructors of our 2 wrappers
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo2).TypeHandle);
        }

        [Fact]
        void CanBePatched()
        {
            
        }
    }
}