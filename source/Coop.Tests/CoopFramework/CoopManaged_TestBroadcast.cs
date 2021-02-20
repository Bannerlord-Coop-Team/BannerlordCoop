using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync.Invokable;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_TestBroadcast
    {
        private readonly Mock<SyncBuffered> m_SyncMock;

        public CoopManaged_TestBroadcast()
        {
            m_SyncMock = new Mock<SyncBuffered>();
        }

        [Fact]
        private void BarIsBroadcastAndSuppressed()
        {
            CoopManagedFoo.Sync = m_SyncMock.Object;
            var foo = new Foo();
            var sync = new CoopManagedFoo(foo);

            // Invoke setter
            foo.Bar = 43;

            // Broadcast was called exactly once with foo as an instance and 43 as argument
            m_SyncMock.Verify(m => m.Broadcast(
                CoopManagedFoo.BarSetter.Id,
                foo,
                It.Is<object[]>(args =>
                    args.Length == 1 &&
                    args[0] is int && (int)
                    args[0] == 43)), Times.Once);

            // The local call to bar was suppressed
            Assert.Equal(42, foo.Bar);
        }

        [Fact]
        private void BazIsBroadcastAndExecuted()
        {
            CoopManagedFoo.Sync = m_SyncMock.Object;
            var foo = new Foo();
            var sync = new CoopManagedFoo(foo);

            // Invoke setter
            foo.Baz = 43;

            // Broadcast was called exactly once with foo as an instance and 43 as argument
            m_SyncMock.Verify(m => m.Broadcast(
                CoopManagedFoo.BazSetter.Id,
                foo,
                It.Is<object[]>(args =>
                    args.Length == 1 &&
                    args[0] is int && (int)
                    args[0] == 43)), Times.Once);

            // The local call to bar was suppressed
            Assert.Equal(43, foo.Baz);
        }

        private class Foo
        {
            public int Bar { get; set; } = 42;
            public int Baz { get; set; } = 42;
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static readonly PatchedInvokable BarSetter = Setter(nameof(Foo.Bar));
            public static readonly PatchedInvokable BazSetter = Setter(nameof(Foo.Baz));

            public static SyncBuffered Sync;

            static CoopManagedFoo()
            {
                // Broadcast local calls on Foo.Bar instead of applying it directly
                When(GameLoop)
                    .Calls(BarSetter)
                    .Broadcast(() => Sync)
                    .Skip();

                // Broadcast local call on Foo.Baz and apply them immediately
                When(GameLoop)
                    .Calls(BazSetter)
                    .Broadcast(() => Sync)
                    .Execute();
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
        }
    }
}