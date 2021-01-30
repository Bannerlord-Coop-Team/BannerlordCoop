using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.MethodCall;
using Coop.Tests.Sync;
using CoopFramework;
using JetBrains.Annotations;
using Network;
using Network.Protocol;
using RailgunNet;
using RailgunNet.Connection.Client;
using RemoteAction;
using Sync;
using Sync.Behaviour;
using Sync.Store;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    [Collection(
        "UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class RPC_Test : IDisposable
    {
        private readonly ISynchronization sync0;
        public RPC_Test()
        {
            Persistence = m_Environment.Persistence ??
                          throw new Exception("Persistence may not be null. Error in test setup.");
            sync0 = new Synchronization(m_Environment.GetClientAccess(ClientId0));
            ManagedFoo.Sync = sync0;
        }

        public void Dispose()
        {
            // MethodPatchFactory<RPC_Test>.UnpatchAll();
            m_Environment.Destroy();
            Foo.LatestArgument = "";
            Foo.NumberOfCalls = 0;
            PendingRequests.Instance.Clear();
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

        private class ManagedFoo : CoopManaged<ManagedFoo, Foo>
        {
            static ManagedFoo()
            {
                When(EActionOrigin.Local)
                    .Calls(Method(nameof(Foo.SyncedMethod)))
                    .Broadcast()
                    .Suppress();
                ApplyStaticPatches();
            }
            public ManagedFoo([NotNull] Foo instance) : base(instance)
            {
            }

            [SyncFactory]
            private static ISynchronization GetSynchronization()
            {
                return Sync;
            }

            public static ISynchronization Sync;
        }

        private readonly TestEnvironment m_Environment = new TestEnvironment(
            2,
            Registry.Client,
            Registry.Server);

        [NotNull] private TestPersistence Persistence { get; }

        private const int ClientId0 = 0;
        private const int ClientId1 = 1;

        private static void AssertIsAckPacket(byte[] payload, ObjectId expectedId)
        {
            Packet packet =
                new PacketReader().Read(new ByteReader(new ArraySegment<byte>(payload)));
            Assert.Equal(EPacket.StoreAck, packet.Type);
            Assert.Equal(4, packet.Payload.Count); // uint32
            uint uiMessageId = new ByteReader(packet.Payload).Binary.ReadUInt32();
            Assert.Equal(expectedId, new ObjectId(uiMessageId));
        }

        private static void AssertNoPendingSends(TestEnvironment env)
        {
            IEnumerable<InMemoryConnection> allConnections =
                env.ConnectionsRaw.ConnectionsClient.Concat(env.ConnectionsRaw.ConnectionsServer);
            foreach (InMemoryConnection connection in allConnections)
            {
                Assert.Empty(connection.SendBuffer);
            }
        }

        [Fact]
        private void CallingPatchedMethodInvokesSyncHandler()
        {
            InMemoryConnection conClientToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            string sMessage = "Hello World";
            int iNumberOfExpectedCalls = 0;

            // Verify initial state
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);
            Assert.Empty(sync0.BroadcastHistory);
            Assert.Empty(conClientToServer.SendBuffer);

            // Call method
            Foo.SyncedMethod(sMessage);
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);

            // Verify that the sync handler was called
            Assert.Single(sync0.BroadcastHistory);
            CallTrace trace = sync0.BroadcastHistory.Peek();
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
        private void ClientReceivesAck()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            RemoteStore client0Store = m_Environment.StoresClient[ClientId0];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient0 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId0];
            InMemoryConnection conClient1ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId1];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // Call method and send event to server
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();
            conClient0ToServer.ExecuteSends();
            CallTrace trace = sync0.BroadcastHistory.Peek();
            ObjectId messageId = trace.Call.Arguments[0].StoreObjectId.Value;

            // Client0 is waiting for the ACK
            Assert.Single(client0Store.State);
            Assert.True(client0Store.State.ContainsKey(messageId));
            Assert.True(client0Store.State[messageId].Sent);
            Assert.False(client0Store.State[messageId].Acknowledged);

            // Let client 1 receive and return ACK
            conServerToClient1.ExecuteSends();
            conClient1ToServer.ExecuteSends();

            // The server will relay ACK to client 0
            Assert.False(client0Store.State[messageId].Acknowledged);
            conServerToClient0.ExecuteSends();
            Assert.True(client0Store.State[messageId].Acknowledged);
        }

        [Fact]
        private void EventIsExecuted()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient0 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId0];
            InMemoryConnection conClient1ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId1];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // Call method and let the store sync
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();
            conClient0ToServer.ExecuteSends();
            conServerToClient1.ExecuteSends();
            conClient1ToServer.ExecuteSends();
            conServerToClient0.ExecuteSends();

            // Broadcast the event itself from server to all clients
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            Persistence.Server.Update();

            // Receive by client 0
            Assert.Equal(0, Foo.NumberOfCalls);
            conServerToClient0.ExecuteSends();
            Assert.Equal(1, Foo.NumberOfCalls);

            // Receive by client 1. Since we're working locally and the hooks point to the same static instance, it will increment the same counter
            conServerToClient1.ExecuteSends();
            Assert.Equal(2, Foo.NumberOfCalls);
        }

        [Fact]
        private void EventIsExecutedOnlyOnce()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            RailClient client1 = Persistence.Clients[ClientId1];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient0 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId0];
            InMemoryConnection conClient1ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId1];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // Call method and let the store sync
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();
            conClient0ToServer.ExecuteSends();
            conServerToClient1.ExecuteSends();
            conClient1ToServer.ExecuteSends();
            conServerToClient0.ExecuteSends();
            Persistence.Server.Update();

            // Update the client a second time before letting the server respond. This will send the event again.
            Assert.Empty(conClient0ToServer.SendBuffer);
            client0.Update();
            Assert.Single(conClient0ToServer.SendBuffer);
            Assert.True(m_Environment.EventQueue.Count == 1);
            conClient0ToServer.ExecuteSends();

            // Server ignored the duplicate event
            Assert.True(m_Environment.EventQueue.Count == 1);

            // Let the server broadcast the event
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            Persistence.Server.Update();

            // Receive on client 0
            Assert.Equal(0, Foo.NumberOfCalls);
            conServerToClient0.ExecuteSends();
            Assert.Equal(1, Foo.NumberOfCalls);

            // Receive by client 1. Since we're working locally and the hooks point to the same static instance, it will increment the same counter
            conServerToClient1.ExecuteSends();
            Assert.Equal(2, Foo.NumberOfCalls);

            // Update the server to initiate a server side resend
            Persistence.Server.Update();
            Assert.Equal(2, Foo.NumberOfCalls);
            conServerToClient1.ExecuteSends();
            Assert.Equal(2, Foo.NumberOfCalls);
            conServerToClient0.ExecuteSends();
            Assert.Equal(2, Foo.NumberOfCalls);
        }

        [Fact]
        private void EventIsReceivedByServer()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            InMemoryConnection conClient0 =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];

            // Call method
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();

            // Verify server side state before receiving the event
            Assert.Equal(0, m_Environment.EventQueue.Count);
            Assert.Empty(m_Environment.StoreServer.Data);

            // Receive
            conClient0.ExecuteSends();
            Assert.Equal(1, m_Environment.EventQueue.Count);
            Assert.Single(m_Environment.StoreServer.Data);

            // Verify the argument was received by the server
            CallTrace trace = sync0.BroadcastHistory.Peek();
            ObjectId messageId = trace.Call.Arguments[0].StoreObjectId.Value;
            Assert.True(m_Environment.StoreServer.Data.ContainsKey(messageId));
        }

        [Fact]
        private void EventIsRepeatedIfNotAcknowledged()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient0 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId0];
            InMemoryConnection conClient1ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId1];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // We cannot really test railguns internals. But we can measure the size of a keep alive
            // and kinda assume whats going on based on this.
            client0.Update();
            Assert.Single(conClient0ToServer.SendBuffer);

            // A keep alive should consist of:
            // - Local tick
            // - ACK tick
            // - ACK latest event id
            // - A few zeros to indicate empty payloads
            // Since we've just started the room the ticks will be lower values which fit into 1 byte each.
            // This is a rough estimate, it doesn't need to be accurate.
            int keepAliveSize = conClient0ToServer.SendBuffer[0].Length;
            Assert.True(keepAliveSize < 10);

            // Call method & finish the store sync to get those messages out of the way
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            conClient0ToServer.ExecuteSends();
            conServerToClient1.ExecuteSends();
            conClient1ToServer.ExecuteSends();
            conServerToClient0.ExecuteSends();

            // Let the client send the event
            Assert.Empty(conClient0ToServer.SendBuffer);
            client0.Update();
            Assert.Single(conClient0ToServer.SendBuffer);
            byte[] payload = conClient0ToServer.SendBuffer[0];
            EPacket eType = PacketReader.DecodePacketType(payload[0]);
            Assert.Equal(EPacket.Persistence, eType);

            // As mentioned earlier, we do not know how railgun structures its packets exactly. But
            // we can assume that a tick sending an event has to larger than a keep alive tick.
            int eventSendSize = conClient0ToServer.SendBuffer[0].Length;
            Assert.True(keepAliveSize < eventSendSize);

            // Updating the room will send the event again since it did not receive an ACK
            client0.Update();
            Assert.Equal(2, conClient0ToServer.SendBuffer.Count);
            int event2SendSize = conClient0ToServer.SendBuffer[1].Length;
            Assert.Equal(eventSendSize, event2SendSize);

            // Let the server receive the event and send an ack
            conClient0ToServer.ExecuteSends();
            Persistence.Server.Update();
            Assert.Single(conServerToClient0.SendBuffer);
            byte[] payloadServerAck = conServerToClient0.SendBuffer[0];
            Assert.Equal(EPacket.Persistence, PacketReader.DecodePacketType(payload[0]));
            conServerToClient0.ExecuteSends();

            // Since the event was acknowledged, the client will no longer attempt to resend it.
            Assert.Empty(conClient0ToServer.SendBuffer);
            client0.Update();
            Assert.Single(conClient0ToServer.SendBuffer);
            Assert.Equal(keepAliveSize, conClient0ToServer.SendBuffer[0].Length);
        }

        [Fact]
        private void IsCorrectRailgunConfig()
        {
            // These tests were written with the assumption that the send rate is 1 on both client & server.
            // If this test fails do not even bother debugging the others, they will fail!
            Assert.Equal(1, RailConfig.SERVER_SEND_RATE);
            Assert.Equal(1, RailConfig.CLIENT_SEND_RATE);
        }

        [Fact]
        private void OtherClientReceivesObject()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            RemoteStore client1Store = m_Environment.StoresClient[ClientId1];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // Call method
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            CallTrace trace = sync0.BroadcastHistory.Peek();
            ObjectId messageId = trace.Call.Arguments[0].StoreObjectId.Value;

            // Sync to server
            client0.Update();
            conClient0ToServer.ExecuteSends();

            // Relay from server to client 1
            Assert.Empty(client1Store.Data);
            conServerToClient1.ExecuteSends();

            // Verify the object was added to client 1 store
            Assert.Single(client1Store.Data);
            Assert.True(client1Store.Data.ContainsKey(messageId));
            object storeData = client1Store.Data[messageId];
            Assert.NotNull(storeData);
            Assert.IsType<string>(storeData);
            string sMessageFromStore = (string) storeData;
            Assert.Equal(sMessage, sMessageFromStore);
        }

        [Fact]
        private void ServerRelaysStoreAdd()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient0 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId0];
            InMemoryConnection conClient1ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId1];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // Call method and send event to server
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();
            conClient0ToServer.ExecuteSends();
            CallTrace trace = sync0.BroadcastHistory.Peek();
            ObjectId messageId = trace.Call.Arguments[0].StoreObjectId.Value;

            // The server relayed the StoreAdd to client 1
            Assert.Single(conServerToClient1.SendBuffer);
            byte[] payloadAdd = conServerToClient1.SendBuffer[0];
            EPacket eTypeAdd = PacketReader.DecodePacketType(payloadAdd[0]);
            Assert.Equal(EPacket.StoreAdd, eTypeAdd);
            Assert.Empty(conServerToClient0.SendBuffer); // But nothing back to client 0

            // Let the client 1 receive the StoreAdd. Client 1 returns an ACK
            conServerToClient1.ExecuteSends();
            Assert.Single(conClient1ToServer.SendBuffer);
            AssertIsAckPacket(conClient1ToServer.SendBuffer[0], messageId);

            // Receive the ACK on the server
            Assert.Empty(conServerToClient0.SendBuffer);
            conClient1ToServer.ExecuteSends();

            // The server will ACK back to client 0
            Assert.Single(conServerToClient0.SendBuffer);
            AssertIsAckPacket(conServerToClient0.SendBuffer[0], messageId);
        }

        [Fact]
        private void ServerWaitsUntilAllArgumentsAreDistributed()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient0 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId0];
            InMemoryConnection conClient1ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId1];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // Call method and send event to server
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();
            conClient0ToServer.ExecuteSends();
            Assert.Equal(1, m_Environment.EventQueue.Count);

            // Updating the event queue will not broadcast anything because the argument was not yet acknowledged by client 1
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            Assert.Equal(1, m_Environment.EventQueue.Count);
            Assert.Single(conServerToClient1.SendBuffer); // StoreAdd to client 1
            Assert.Empty(conServerToClient0.SendBuffer);

            // Let the client 1 receive the StoreAdd & return ACK to server
            conServerToClient1.ExecuteSends();
            Assert.Single(conClient1ToServer.SendBuffer);
            conClient1ToServer.ExecuteSends();

            // Event can now be broadcast
            Assert.Equal(1, m_Environment.EventQueue.Count);
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            Assert.Equal(0, m_Environment.EventQueue.Count);
        }

        [Fact]
        private void SyncHandlersGeneratesEvent()
        {
            // Call method
            string sMessage = "Hello World";
            int iNumberOfExpectedCalls = 0;
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);
            Assert.Empty(sync0.BroadcastHistory);
            Foo.SyncedMethod(sMessage);
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);

            // Verify the SyncHandler was called at all
            Assert.Single(sync0.BroadcastHistory);

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
