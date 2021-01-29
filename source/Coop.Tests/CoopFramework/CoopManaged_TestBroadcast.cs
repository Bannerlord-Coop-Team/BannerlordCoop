using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestBroadcast
    {
        class Foo
        {
            public int Bar { get; set; } = 42;
            public int Baz { get; set; } = 42;
        }
        
        private Mock<ISynchronization> m_SyncMock;

        public CoopManaged_TestBroadcast()
        {
            m_SyncMock = new Mock<ISynchronization>();
        }

        class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static MethodAccess BarSetter = Setter(nameof(Foo.Bar));
            public static MethodAccess BazSetter = Setter(nameof(Foo.Baz));
            static CoopManagedFoo()
            {
                // Broadcast local calls on Foo.Bar instead of applying it directly
                When(ETriggerOrigin.Local)
                    .Calls(BarSetter)
                    .Broadcast()
                    .Suppress();
                
                // Broadcast local call on Foo.Baz and apply them immediately
                When(ETriggerOrigin.Local)
                    .Calls(BazSetter)
                    .Broadcast()
                    .Execute();
            }

            public CoopManagedFoo(ISynchronization sync, [NotNull] Foo instance) : base(instance)
            {
                m_Sync = sync;
            }

            private readonly ISynchronization m_Sync;

            [SyncFactory]
            private ISynchronization GetSynchronization()
            {
                return m_Sync;
            }
        }

        [Fact]
        void BarIsBroadcastAndSuppressed()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(m_SyncMock.Object, foo);

            // Invoke setter
            foo.Bar = 43;
            
            // Broadcast was called exactly once with foo as an instance and 43 as argument
            m_SyncMock.Verify((m => m.Broadcast(
                CoopManagedFoo.BarSetter.Id, 
                foo, 
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] is int && (int) 
                    args[0] == 43))), Times.Once);
            
            // The local call to bar was suppressed
            Assert.Equal(42, foo.Bar);
        }
        
        [Fact]
        void BazIsBroadcastAndExecuted()
        {
            Foo foo = new Foo();
            CoopManagedFoo sync = new CoopManagedFoo(m_SyncMock.Object, foo);

            // Invoke setter
            foo.Baz = 43;
            
            // Broadcast was called exactly once with foo as an instance and 43 as argument
            m_SyncMock.Verify((m => m.Broadcast(
                CoopManagedFoo.BazSetter.Id, 
                foo, 
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] is int && (int) 
                    args[0] == 43))), Times.Once);
            
            // The local call to bar was suppressed
            Assert.Equal(43, foo.Baz);
        }
    }
}