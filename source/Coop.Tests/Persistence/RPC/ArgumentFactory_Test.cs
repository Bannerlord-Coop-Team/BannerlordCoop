using System;
using Coop.Mod.Persistence.RemoteAction;
using Coop.Tests.Sync;
using RailgunNet.System.Encoding;
using RemoteAction;
using Sync.Store;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    public class ArgumentFactory_Test
    {
        public ArgumentFactory_Test()
        {
            m_StoreClient0 = m_Environment.StoresClient[0];
            m_StoreClient1 = m_Environment.StoresClient[1];
        }

        private readonly TestEnvironment m_Environment = new TestEnvironment(2);
        private readonly RemoteStore m_StoreClient0;
        private readonly RemoteStore m_StoreClient1;

        private readonly RailBitBuffer buffer = new RailBitBuffer();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        private void NullCanBeSerialized(bool byValue)
        {
            // Create
            Argument arg = ArgumentFactory.Create(m_StoreClient0, null, byValue);
            Assert.Empty(m_StoreClient0.Data);
            Assert.Equal(
                EventArgType.Null,
                arg.EventType); // Regardless of byValue because its a value type!
            Assert.False(arg.MbGUID.HasValue);
            Assert.False(arg.Int.HasValue);
            Assert.False(arg.StoreObjectId.HasValue);

            // Serialize
            buffer.EncodeEventArg(arg);

            // Deserialize
            Argument argDeserialized = buffer.DecodeEventArg();
            Assert.Equal(arg, argDeserialized);

            // Resolve
            object nullResolved = ArgumentFactory.Resolve(m_StoreClient0, argDeserialized);
            Assert.Null(nullResolved);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        private void IntCanBeSerialized(bool byValue)
        {
            // Create
            int i = 42;
            Argument arg = ArgumentFactory.Create(m_StoreClient0, i, byValue);
            Assert.Empty(m_StoreClient0.Data);
            Assert.Equal(
                EventArgType.Int,
                arg.EventType); // Regardless of byValue because its a value type!
            Assert.False(arg.MbGUID.HasValue);
            Assert.True(arg.Int.HasValue);
            Assert.False(arg.StoreObjectId.HasValue);

            // Serialize
            buffer.EncodeEventArg(arg);

            // Deserialize
            Argument argDeserialized = buffer.DecodeEventArg();
            Assert.Equal(arg, argDeserialized);

            // Resolve
            object resolved = ArgumentFactory.Resolve(m_StoreClient0, argDeserialized);
            Assert.NotNull(resolved);
            Assert.IsType<int>(resolved);
            Assert.Equal(i, resolved);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        [InlineData(-1000)]
        [InlineData(float.MinValue)]
        [InlineData(float.MaxValue)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(float.PositiveInfinity)]
        private void FloatCanBeSerialized(float fValue)
        {
            // Create
            Argument arg = ArgumentFactory.Create(m_StoreClient0, fValue, false);
            Assert.Empty(m_StoreClient0.Data);
            Assert.Equal(
                EventArgType.Float,
                arg.EventType); // Regardless of byValue because its a value type!
            Assert.True(arg.Float.HasValue);

            // Serialize
            buffer.EncodeEventArg(arg);

            // Deserialize
            Argument argDeserialized = buffer.DecodeEventArg();
            Assert.Equal(arg, argDeserialized);

            // Resolve
            object resolved = ArgumentFactory.Resolve(m_StoreClient0, argDeserialized);
            Assert.NotNull(resolved);
            Assert.IsType<float>(resolved);
            Assert.Equal(fValue, resolved);
        }

        [Fact]
        private void ObjectIsRemovedFromStoreAfterResolve()
        {
            // Create
            DateTime time = DateTime.Now; // Just an arbitrary type that supports serialization.
            Argument arg = ArgumentFactory.Create(m_StoreClient0, time, true);
            Assert.Single(m_StoreClient0.Data);
            Assert.Equal(EventArgType.StoreObjectId, arg.EventType);
            Assert.True(arg.StoreObjectId.HasValue);
            Assert.Contains(arg.StoreObjectId.Value, m_StoreClient0.Data);

            // Serialize
            buffer.EncodeEventArg(arg);

            // Deserialize
            Argument argDeserialized = buffer.DecodeEventArg();
            Assert.Equal(arg, argDeserialized);

            // Resolve
            object resolved = ArgumentFactory.Resolve(m_StoreClient0, argDeserialized);
            Assert.NotNull(resolved);
            Assert.Empty(m_StoreClient0.Data);
        }

        [Fact]
        private void StoreObjectIdResolveThrowsIfNotYetSynchronized()
        {
            // Create
            DateTime time = DateTime.Now; // Just an arbitrary type that supports serialization.
            Argument arg = ArgumentFactory.Create(m_StoreClient0, time, true);
            Assert.Equal(EventArgType.StoreObjectId, arg.EventType);
            Assert.False(arg.MbGUID.HasValue);
            Assert.False(arg.Int.HasValue);
            Assert.True(arg.StoreObjectId.HasValue);
            Assert.Contains(arg.StoreObjectId.Value, m_StoreClient0.Data);

            // Serialize
            buffer.EncodeEventArg(arg);

            // Deserialize
            Argument argDeserialized = buffer.DecodeEventArg();
            Assert.Equal(arg, argDeserialized);

            // Resolve on client 0 works since that store was used for the create
            object resolved = ArgumentFactory.Resolve(m_StoreClient0, argDeserialized);
            Assert.NotNull(resolved);
            Assert.IsType<DateTime>(resolved);
            Assert.Equal(time, resolved);

            // Resolve on client 1 fails because the store was not synchronized
            Assert.Throws<ArgumentException>(
                () => ArgumentFactory.Resolve(m_StoreClient1, argDeserialized));

            // Sync the store
            m_Environment.ExecuteSendsClients(); // Client0 -> Server
            m_Environment.ExecuteSendsServer(); // Server -> Client 1

            // Now client 1 can resolve the argument
            object resolvedClient1 = ArgumentFactory.Resolve(m_StoreClient1, argDeserialized);
            Assert.NotNull(resolvedClient1);
            Assert.IsType<DateTime>(resolvedClient1);
            Assert.Equal(time, resolvedClient1);
        }

        enum ETest
        {
            First,
            Second,
            Third
        }
        
        [Fact]
        void EnumCanBeUsedAsArgument()
        {
            // Create
            ETest transferedValue = ETest.Second;
            Argument arg = ArgumentFactory.Create(m_StoreClient0, transferedValue, true);
            Assert.Equal(EventArgType.Int, arg.EventType);
            Assert.False(arg.MbGUID.HasValue);
            Assert.True(arg.Int.HasValue);
            Assert.False(arg.StoreObjectId.HasValue);

            // Serialize
            buffer.EncodeEventArg(arg);

            // Deserialize
            Argument argDeserialized = buffer.DecodeEventArg();
            Assert.Equal(arg, argDeserialized);

            // Resolve on client 0 works since that store was used for the create
            object resolved = ArgumentFactory.Resolve(m_StoreClient0, argDeserialized);
            Assert.NotNull(resolved);
            Assert.IsType<int>(resolved);
            Assert.Equal(transferedValue, (ETest) resolved);

            // client 1 can resolve the argument
            object resolvedClient1 = ArgumentFactory.Resolve(m_StoreClient1, argDeserialized);
            Assert.NotNull(resolvedClient1);
            Assert.IsType<int>(resolvedClient1);
            Assert.Equal(transferedValue, (ETest) resolvedClient1);
        }
    }
}
