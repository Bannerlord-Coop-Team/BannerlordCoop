using System;
using System.Runtime.CompilerServices;
using CoopFramework;
using HarmonyLib;
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
                When(EActionOrigin.Local)
                    .Calls(BarSetter)
                    .DelegateTo((managedFoo, call) =>
                    {
                        var instance = (managedFoo as CoopManagedFoo);
                        instance.WasCalled = true;
                        if (instance.BarHandler != null)
                        {
                            return instance.BarHandler(call);
                        }

                        return ECallPropagation.CallOriginal;
                    });
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }

            public Func<IPendingMethodCall, ECallPropagation> BarHandler;
            public bool WasCalled = false;
        }
        
        class CoopManagedFoo2 : CoopManaged<CoopManagedFoo2, Foo>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            static CoopManagedFoo2()
            {
                When(EActionOrigin.Local)
                    .Calls(BarSetter)
                    .DelegateTo((managedFoo, call) =>
                    {
                        var instance = (managedFoo as CoopManagedFoo2);
                        instance.WasCalled = true;
                        if (instance.BarHandler != null)
                        {
                            return instance.BarHandler(call);
                        }

                        return ECallPropagation.CallOriginal;
                    });
            }

            public CoopManagedFoo2([NotNull] Foo instance) : base(instance)
            {
            }
            
            public Func<IPendingMethodCall, ECallPropagation> BarHandler;
            public bool WasCalled = false;
        }

        static CoopManaged_IncrementalPatching()
        {
            // Initialize the static constructors of our 2 wrappers
            RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(CoopManagedFoo2).TypeHandle);
            
        }

        [Fact]
        void CanBePatched()
        {
            Foo foo = new Foo();
            CoopManagedFoo fooManaged = new CoopManagedFoo(foo);
            CoopManagedFoo2 fooManaged2 = new CoopManagedFoo2(foo);
            foo.Bar = 43;
            Assert.True(fooManaged.WasCalled);
            Assert.True(fooManaged2.WasCalled);
        }
        
        [Fact]
        void PatchesAreCalledInOrderFIFO()
        {
            Foo foo = new Foo();
            CoopManagedFoo fooManaged = new CoopManagedFoo(foo);
            CoopManagedFoo2 fooManaged2 = new CoopManagedFoo2(foo);
            
            // configure fooManaged to suppress the call
            fooManaged.BarHandler = call => ECallPropagation.Suppress;

            foo.Bar = 43;
            Assert.True(fooManaged.WasCalled);
            Assert.False(fooManaged2.WasCalled);
            Assert.Equal(42, foo.Bar); // Not changed!
            
            // configure fooManaged to propagate the call so it reaches fooManaged2
            fooManaged.WasCalled = false;
            fooManaged.BarHandler = call => ECallPropagation.CallOriginal;
            fooManaged2.BarHandler = call => ECallPropagation.Suppress;
            
            foo.Bar = 43;
            Assert.True(fooManaged2.WasCalled);
            Assert.True(fooManaged.WasCalled);
        }
    }
}