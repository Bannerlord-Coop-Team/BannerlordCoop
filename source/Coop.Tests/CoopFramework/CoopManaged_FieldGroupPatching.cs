﻿using System.Collections.Generic;
using CoopFramework;
using JetBrains.Annotations;
using Moq;
using Sync.Value;
using Xunit;

namespace Coop.Tests.CoopFramework
{
    [Collection("UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class CoopManaged_FieldGroupPatching
    {
        private readonly Mock<SyncBuffered> m_SyncMock;
        private FieldChangeBuffer m_LatestBuffer;

        public CoopManaged_FieldGroupPatching()
        {
            m_SyncMock = new Mock<SyncBuffered>();
            m_SyncMock.Setup(h => h.Broadcast(It.IsAny<FieldChangeBuffer>()))
                .Callback<FieldChangeBuffer>(buffer => { m_LatestBuffer = buffer; });
            CoopManagedFoo.Sync = m_SyncMock.Object;
        }

        [Fact]
        private void DoesRevertFieldChangeThroughProperty()
        {
            var foo = new Foo();
            var fooManaged = new CoopManagedFoo(foo);
            Assert.Equal(0, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);

            // Change through property
            foo.SetBoth(1, 43);
            Assert.Equal(0, foo.m_Bar);
            Assert.Equal(42, foo.m_Baz);

            // Verify that the change was recorded to the field change buffer
            Assert.NotNull(m_LatestBuffer);
            Assert.Equal(1, m_LatestBuffer.Count());
            var changes = m_LatestBuffer.FetchChanges();
            Assert.Single(changes);
            Assert.True(changes.ContainsKey(CoopManagedFoo.AccessGroup));

            // Verify the change was recorded on the `foo` instance
            var instanceChanges = changes[CoopManagedFoo.AccessGroup];
            Assert.Single(instanceChanges);
            Assert.True(instanceChanges.ContainsKey(foo));

            // Verify that the fields where packed as defined by CoopManaged.Group
            var fooChange = instanceChanges[foo];
            Assert.IsType<List<object>>(fooChange.OriginalValue);
            Assert.IsType<List<object>>(fooChange.RequestedValue);
            var valueBefore = (List<object>) fooChange.OriginalValue;
            var requestedValue = (List<object>) fooChange.RequestedValue;
            Assert.Equal(2, valueBefore.Count);
            Assert.Equal(2, requestedValue.Count);
            Assert.IsType<int>(valueBefore[0]);
            Assert.IsType<int>(valueBefore[1]);
            Assert.IsType<int>(requestedValue[0]);
            Assert.IsType<int>(requestedValue[1]);

            // Verify the actual field values
            Assert.Equal(0, (int) valueBefore[0]); // m_Bar
            Assert.Equal(42, (int) valueBefore[1]); // m_Baz
            Assert.Equal(1, (int) requestedValue[0]); // m_Bar
            Assert.Equal(43, (int) requestedValue[1]); // m_Baz
        }

        private class Foo
        {
            public int m_Bar;
            public int m_Baz = 42;

            public void SetBoth(int bar, int baz)
            {
                m_Bar = bar;
                m_Baz = baz;
            }
        }

        private class CoopManagedFoo : CoopManaged<CoopManagedFoo, Foo>
        {
            public static readonly FieldAccessGroup<Foo, List<object>> AccessGroup =
                new FieldAccessGroup<Foo, List<object>>(
                    new[]
                    {
                        Field<int>(nameof(Foo.m_Bar)),
                        Field<int>(nameof(Foo.m_Baz))
                    });

            public static SyncBuffered Sync;

            static CoopManagedFoo()
            {
                When(GameLoop)
                    .Changes(AccessGroup)
                    .Through(Method(nameof(Foo.SetBoth)))
                    .Broadcast(() => Sync)
                    .Revert();
            }

            public CoopManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }
        }
    }
}