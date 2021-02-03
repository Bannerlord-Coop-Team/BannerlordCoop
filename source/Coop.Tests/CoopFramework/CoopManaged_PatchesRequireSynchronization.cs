using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_PatchesRequireSynchronization
    {
        public CoopManaged_PatchesRequireSynchronization()
        {
        }
        
        [Fact]
        private void PatchesAreInactiveWithoutSync()
        {
            var foo = new Foo();
            var managedFoo = new CoopManagedFoo(null, foo);
            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);
            
            foo.SetBar(43);
            foo.Baz = 43;

            Assert.Equal(43, foo.Bar);
            Assert.Equal(43, foo.Baz);
        }
        
        [Fact]
        private void PatchesAreActiveWithSync()
        {
            
            var foo = new Foo();
            var managedFoo = new CoopManagedFoo(new Mock<ISynchronization>().Object, foo);
            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);
            
            foo.SetBar(43);
            foo.Baz = 43;

            Assert.Equal(42, foo.Bar);
            Assert.Equal(42, foo.Baz);
        }
        
        private class Foo
        {
            public int Bar = 42;

            public int Baz { get; set; } = 42;
            
            public void SetBar(int i)
            {
                Bar = i;
            }
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            static CoopManagedFoo()
            {
                When(GameLoop)
                    .Changes(Field<int>(nameof(Foo.Bar)))
                    .Through(Method(nameof(Foo.SetBar)))
                    .Revert();
                When(GameLoop)
                    .Calls(Setter(nameof(Foo.Baz)))
                    .Skip();
            }
            
            public CoopManagedFoo(ISynchronization sync, [NotNull] Foo instance) : base(instance)
            {
                m_Sync = sync;
            }

            [SyncFactory]
            private ISynchronization GetSynchronization()
            {
                return m_Sync;
            }

            private readonly ISynchronization m_Sync;
        }
    }
}