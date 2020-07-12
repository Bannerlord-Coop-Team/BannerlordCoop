using System;
using System.Linq;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RPC;
using Coop.Tests.Sync;
using JetBrains.Annotations;
using Network;
using Network.Protocol;
using RailgunNet.Connection.Client;
using Sync;
using Sync.Store;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    [Collection("Sequential")]
    public class EventMethodCall_Test : IDisposable
    {
        public EventMethodCall_Test()
        {
            // Init patch
            MethodPatch Patch = new MethodPatch(typeof(Foo)).Intercept(nameof(Foo.SyncedMethod));
            Persistence = m_Environment.Persistence ??
                          throw new Exception("Persistence may not be null. Error in test setup.");
            Persistence.SyncHandlers.Register(
                Patch.Methods,
                m_Environment.GetClientAccess(ClientId0));
            Access = Patch.Methods.First();
            SyncHandler = Persistence.SyncHandlers.Handlers.Find(o => o.MethodAccess == Access);
            if (SyncHandler == null)
            {
                throw new Exception("Error during setup.");
            }
        }

        public void Dispose()
        {
            m_Environment.Destroy();
        }

        private class Foo
        {
            [ThreadStatic] public static string LatestArgument;
            [ThreadStatic] public static int NumberOfCalls;

            public static void SyncedMethod(string sSomeArgument)
            {
                ++NumberOfCalls;
                LatestArgument = sSomeArgument;
            }
        }

        private readonly TestEnvironment m_Environment = new TestEnvironment(
            2,
            Registry.Client,
            Registry.Server);

        [NotNull] private TestPersistence Persistence { get; }
        [NotNull] private MethodAccess Access { get; }

        [NotNull] private MethodCallSyncHandler SyncHandler { get; }

        private const int ClientId0 = 0;

        [Fact]
        private void CallingPatchedMethodInvokesSyncHandler()
        {
            InMemoryConnection conClientToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            string sMessage = "Hello World";
            int iNumberOfExpectedCalls = 0;

            // Verify initial state
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);
            Assert.Empty(SyncHandler.Stats.History);
            Assert.Empty(conClientToServer.SendBuffer);

            // Call method
            Foo.SyncedMethod(sMessage);
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);

            // Verify that the sync handler was called
            Assert.Single(SyncHandler.Stats.History);
            MethodCallSyncHandler.Statistics.Trace trace = SyncHandler.Stats.History.Peek();
            Assert.Equal(Persistence.Rooms[ClientId0].Tick, trace.Tick);
            Assert.Equal(
                Argument.Null,
                trace.Call.Instance); // Since it's a static call the instance is null

            // Verify the RPC argument
            Assert.Single(trace.Call.Arguments);
            Argument arg0 = trace.Call.Arguments[ClientId0];
            Assert.Equal(
                EventArgType.StoreObjectId,
                arg0.EventType); // strings are always put into the store
            Assert.True(arg0.StoreObjectId.HasValue);
            ObjectId messageId = arg0.StoreObjectId.Value;

            // Verify arg0 was put into store
            RemoteStore store = m_Environment.StoresClient[ClientId0];
            Assert.Single(store.Data);
            Assert.True(store.Data.ContainsKey(messageId));
            object storeData = store.Data[messageId];
            Assert.NotNull(storeData);
            Assert.IsType<string>(storeData);
            string sMessageFromStore = (string) storeData;
            Assert.Equal(sMessage, sMessageFromStore);

            // Adding it to the store sent a StoreAdd message containing the serialized string
            Assert.Single(conClientToServer.SendBuffer);
            byte[] payload = conClientToServer.SendBuffer[0];
            EPacket eType = PacketReader.DecodePacketType(payload[0]);
            Assert.Equal(EPacket.StoreAdd, eType);

            // Verify the payload in StoreAdd
            Packet packet =
                new PacketReader().Read(new ByteReader(new ArraySegment<byte>(payload)));
            long expectedPayloadSize = TestUtils.GetSerializedSize(sMessage);
            Assert.Equal(expectedPayloadSize, packet.Payload.Count);
        }

        [Fact]
        private void SyncHandlersGeneratesEvent()
        {
            // Call method
            string sMessage = "Hello World";
            int iNumberOfExpectedCalls = 0;
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);
            Assert.Empty(SyncHandler.Stats.History);
            Foo.SyncedMethod(sMessage);
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);

            // Verify the SyncHandler was called at all
            Assert.Single(SyncHandler.Stats.History);
            MethodCallSyncHandler.Statistics.Trace trace = SyncHandler.Stats.History.Peek();

            // There should be an outgoing event in the clients queue. We should be able to observe the event being sent on the next update.
            RailClient client0 = Persistence.Clients[ClientId0];
            InMemoryConnection conClientToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            Assert.Single(
                conClientToServer.SendBuffer); // There's already a StoreAdd packet in there
            client0.Update();
            Assert.Equal(2, conClientToServer.SendBuffer.Count);
            EPacket eType = PacketReader.DecodePacketType(conClientToServer.SendBuffer.Last()[0]);
            Assert.Equal(EPacket.Persistence, eType);
        }
    }
}
