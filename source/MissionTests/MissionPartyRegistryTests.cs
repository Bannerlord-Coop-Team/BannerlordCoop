using GameInterface.Missions;
using GameInterface.Services.Entity;
using System;
using Xunit;

namespace MissionTests
{
    public class MissionPartyRegistryTests
    {
        private const string Client = "client-1";
        private const string Host = "host";

        private static MissionPartyRegistry NewRegistry(string localId)
        {
            var idProvider = new ControllerIdProvider();
            idProvider.SetControllerId(localId);
            return new MissionPartyRegistry(idProvider);
        }

        private static Guid RegisterParty(MissionPartyRegistry registry, string owner)
        {
            var partyId = Guid.NewGuid();
            Assert.True(registry.TryRegisterParty(partyId, owner, Guid.NewGuid(), new[] { Guid.NewGuid(), Guid.NewGuid() }, out _));
            return partyId;
        }

        [Fact]
        public void NewParty_IsControlledByOwner()
        {
            var registry = NewRegistry(Client);
            var partyId = RegisterParty(registry, Client);

            Assert.True(registry.IsLocallyControlled(partyId));
            Assert.True(registry.TryGetParty(partyId, out var party));
            Assert.Equal(Client, party.OriginalOwner);
            Assert.Equal(Client, party.CurrentAuthority);
        }

        [Fact]
        public void Disconnect_TransfersAuthorityToHost_KeepsOriginalOwner()
        {
            // Local node is the host.
            var registry = NewRegistry(Host);
            var partyId = RegisterParty(registry, Client);

            Assert.False(registry.IsLocallyControlled(partyId)); // host does not control the client's party yet

            Assert.True(registry.TryTransferAuthority(partyId, Host));

            Assert.True(registry.IsLocallyControlled(partyId)); // host now drives it
            Assert.True(registry.TryGetParty(partyId, out var party));
            Assert.Equal(Client, party.OriginalOwner);          // identity preserved so rejoin can find it
            Assert.Equal(Host, party.CurrentAuthority);
            Assert.Contains(party, registry.GetPartiesControlledBy(Host));
            Assert.Contains(party, registry.GetPartiesOwnedBy(Client));
        }

        [Fact]
        public void Rejoin_ReturnsAuthorityToOriginalOwner()
        {
            var registry = NewRegistry(Host);
            var partyId = RegisterParty(registry, Client);
            Assert.True(registry.TryTransferAuthority(partyId, Host)); // disconnect

            // Rejoin: locate the client's party by original owner, hand it back.
            var owned = registry.GetPartiesOwnedBy(Client);
            Assert.Single(owned);
            Assert.True(registry.TryTransferAuthority(owned[0].PartyId, Client));

            Assert.False(registry.IsLocallyControlled(partyId));     // host no longer controls it
            Assert.Empty(registry.GetPartiesControlledBy(Host));
            Assert.Equal(Client, owned[0].CurrentAuthority);
        }

        [Fact]
        public void Transfer_UnknownParty_Fails()
        {
            var registry = NewRegistry(Host);
            Assert.False(registry.TryTransferAuthority(Guid.NewGuid(), Host));
        }

        [Fact]
        public void DuplicateParty_Fails()
        {
            var registry = NewRegistry(Host);
            var partyId = Guid.NewGuid();
            Assert.True(registry.TryRegisterParty(partyId, Client, Guid.NewGuid(), null, out _));
            Assert.False(registry.TryRegisterParty(partyId, Client, Guid.NewGuid(), null, out _));
        }

        [Fact]
        public void RemoveParty_ClearsIndexes()
        {
            var registry = NewRegistry(Host);
            var partyId = RegisterParty(registry, Client);

            Assert.True(registry.RemoveParty(partyId));
            Assert.False(registry.TryGetParty(partyId, out _));
            Assert.Empty(registry.GetPartiesOwnedBy(Client));
            Assert.False(registry.RemoveParty(partyId));
        }
    }
}
