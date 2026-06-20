using Common.Messaging;
using Common.Serialization;
using Coop.Core.Server.Connections.Messages;
using ProtoBuf;
using Xunit;

namespace Coop.IntegrationTests.Serialization
{
    /// <summary>
    /// Reproduces the "Time controls disabled, N players are currently joining" hang.
    ///
    /// The server clears the loading/pause lock only when each joining client's
    /// <see cref="NetworkPlayerCampaignEntered"/> is received and moves that connection out of
    /// LoadingState. In the live <c>Coop_server.log</c> that message never arrives: it deserializes
    /// into the wrong type (<c>GameInterface.Surrogates.ExplanationLineSurrogate</c>), throws an
    /// InvalidCastException the poller swallows, and both connections stay in LoadingState forever.
    ///
    /// Root cause: <see cref="SerializableTypeMapper"/> gives each type a wire id equal to its
    /// index in an alphabetically-sorted list of every <c>[ProtoContract]</c> type the node knows
    /// about (its constructor's scan, plus later <c>AddTypes</c> calls from AutoRegistryFactory /
    /// AutoSyncPatcher). The id of a type therefore depends on the WHOLE set. If the client and
    /// server end up with even slightly different sets, the indices shift and the ids no longer
    /// line up between the two processes.
    ///
    /// These tests build two independent mappers (standing in for the two processes). The
    /// existing integration/E2E harnesses can't surface this: their routers deliver IMessages
    /// in-process — by reference (Coop.IntegrationTests) or round-tripped through one shared
    /// mapper (E2E.Tests) — so there is never a second node with a different map.
    /// </summary>
    public class NetworkTypeMappingTests
    {
        /// <summary>
        /// Sanity / positive control: two nodes that registered the same set of types agree, so a
        /// message round-trips back to its own type. This is the case the current harness exercises,
        /// and it passes — which is why the bug looks like "it should be working fine".
        /// </summary>
        [Fact]
        public void SameRegistrations_MessageRoundTripsToItsOwnType()
        {
            var serverMapper = new SerializableTypeMapper();
            var clientMapper = new SerializableTypeMapper();

            var serverSerializer = new ProtoBufSerializer(serverMapper);
            var clientSerializer = new ProtoBufSerializer(clientMapper);

            byte[] wire = serverSerializer.Serialize(new NetworkPlayerCampaignEntered());
            var received = clientSerializer.Deserialize<IMessage>(wire);

            Assert.IsType<NetworkPlayerCampaignEntered>(received);
        }

        /// <summary>
        /// The core defect: a type's wire id must not depend on what else a node has registered.
        /// Registering one extra serializable type renumbers existing types, so two nodes with
        /// different registrations will disagree on the wire.
        /// </summary>
        [Fact]
        public void RegisteringAnExtraType_MustNotChangeAnExistingTypesId()
        {
            var mapper = new SerializableTypeMapper();

            Assert.True(mapper.TryGetId(typeof(NetworkPlayerCampaignEntered), out var idBefore));

            // A node registers one more serializable type at runtime — exactly what
            // AutoRegistryFactory does per synced object type (typeof(NetworkCreateInstance<T>) etc.).
            mapper.AddTypes(new[] { typeof(global::AAA_SerializationProbe.EarlySortingProbe<int>) });

            Assert.True(mapper.TryGetId(typeof(NetworkPlayerCampaignEntered), out var idAfter));

            Assert.Equal(idBefore, idAfter);
        }

        /// <summary>
        /// End-to-end repro of the log: the server serializes the campaign-entered message, the
        /// client (which registered a different type set) deserializes it — and gets the wrong type
        /// back, throwing the same InvalidCastException seen in production.
        /// </summary>
        [Fact]
        public void DifferingRegistrations_MessageStillRoundTripsAcrossNodes()
        {
            var serverMapper = new SerializableTypeMapper();
            var clientMapper = new SerializableTypeMapper();

            // The client knows one serializable type the server doesn't (different load order or a
            // per-node registration, e.g. the Missions assembly's messages). With position-based
            // ids this renumbers everything sorted after it.
            clientMapper.AddTypes(new[] { typeof(global::AAA_SerializationProbe.EarlySortingProbe<int>) });

            var serverSerializer = new ProtoBufSerializer(serverMapper);
            var clientSerializer = new ProtoBufSerializer(clientMapper);

            // Server announces a player entered the campaign (the message that releases the lock).
            byte[] wire = serverSerializer.Serialize(new NetworkPlayerCampaignEntered());

            // Client decodes it, exactly as MessagePacketHandler does (deserialize as IMessage).
            var received = clientSerializer.Deserialize<IMessage>(wire);

            Assert.IsType<NetworkPlayerCampaignEntered>(received);
        }

        /// <summary>
        /// A type whose assembly loads after the mapper was built (e.g. a Missions message on the
        /// server, which loads Missions.dll lazily) is not collected at construction. The mapper must
        /// still produce its stable id on the serialize side and resolve it back on the receive side,
        /// rather than throwing "not registered". Modelled with a closed generic, which the
        /// construction-time scan never collects (only the open generic is).
        /// </summary>
        [Fact]
        public void TypeNotCollectedAtConstruction_StillSerializesAndResolves()
        {
            var mapper = new SerializableTypeMapper();
            var lateType = typeof(global::AAA_SerializationProbe.EarlySortingProbe<int>);

            // Serialize side: must not throw / return false.
            Assert.True(mapper.TryGetId(lateType, out var id));

            // Receive side: the same id resolves back to the same type.
            Assert.True(mapper.TryGetType(id, out var resolved));
            Assert.Equal(lateType, resolved);
        }
    }
}

namespace AAA_SerializationProbe
{
    /// <summary>
    /// Stand-in for a serializable type that only one node has registered. It sits in an
    /// alphabetically-early namespace so that, under the current position-based id scheme, adding
    /// it shifts the ids of the real network messages. A closed generic is used deliberately: the
    /// open generic is auto-collected by every node, but a specific instantiation is only added by
    /// an explicit <c>AddTypes</c> call — mirroring AutoRegistryFactory's per-type registrations.
    /// </summary>
    [ProtoContract]
    public class EarlySortingProbe<T> { }
}
