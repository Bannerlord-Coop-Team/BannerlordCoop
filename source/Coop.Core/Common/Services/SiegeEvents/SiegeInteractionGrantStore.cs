using LiteNetLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Siege;

namespace Coop.Core.Common.Services.SiegeEvents;

public interface ISiegeInteractionGrantStore
{
    string CreateInteractionId();
    void Grant(
        NetPeer peer,
        string interactionId,
        string partyId,
        string settlementId,
        BesiegerCamp presentedCamp);
    bool TryConsume(
        NetPeer peer,
        string interactionId,
        string partyId,
        string settlementId,
        BesiegerCamp presentedCamp);
    void Revoke(NetPeer peer);
    void RevokeParty(string partyId);
    void RecordLocal(string interactionId, string partyId, string settlementId);
    bool TryConsumeLocal(string partyId, string settlementId, out string interactionId);
    void ClearLocal(string partyId);
}

internal sealed class SiegeInteractionGrantStore : ISiegeInteractionGrantStore
{
    private readonly object sync = new object();
    private readonly Dictionary<NetPeer, SiegeInteractionGrant> remoteGrants =
        new Dictionary<NetPeer, SiegeInteractionGrant>();
    private SiegeInteractionGrant localGrant;

    public string CreateInteractionId() => Guid.NewGuid().ToString("N");

    public void Grant(
        NetPeer peer,
        string interactionId,
        string partyId,
        string settlementId,
        BesiegerCamp presentedCamp)
    {
        if (peer == null || string.IsNullOrEmpty(interactionId))
            return;

        lock (sync)
        {
            remoteGrants[peer] = new SiegeInteractionGrant(
                interactionId,
                partyId,
                settlementId,
                presentedCamp);
        }
    }

    public bool TryConsume(
        NetPeer peer,
        string interactionId,
        string partyId,
        string settlementId,
        BesiegerCamp presentedCamp)
    {
        lock (sync)
        {
            if (peer == null ||
                !remoteGrants.TryGetValue(peer, out var grant) ||
                !grant.Matches(
                    interactionId,
                    partyId,
                    settlementId,
                    presentedCamp))
            {
                return false;
            }

            remoteGrants.Remove(peer);
            return true;
        }
    }

    public void Revoke(NetPeer peer)
    {
        if (peer == null)
            return;

        lock (sync)
        {
            remoteGrants.Remove(peer);
        }
    }

    public void RevokeParty(string partyId)
    {
        if (string.IsNullOrEmpty(partyId))
            return;

        lock (sync)
        {
            var peers = new List<NetPeer>();
            foreach (var grant in remoteGrants)
            {
                if (grant.Value.PartyId == partyId)
                    peers.Add(grant.Key);
            }

            foreach (var peer in peers)
                remoteGrants.Remove(peer);
        }
    }

    public void RecordLocal(string interactionId, string partyId, string settlementId)
    {
        lock (sync)
        {
            localGrant = string.IsNullOrEmpty(interactionId)
                ? null
                : new SiegeInteractionGrant(
                    interactionId,
                    partyId,
                    settlementId,
                    presentedCamp: null);
        }
    }

    public bool TryConsumeLocal(string partyId, string settlementId, out string interactionId)
    {
        lock (sync)
        {
            interactionId = null;
            if (localGrant == null || !localGrant.Matches(partyId, settlementId))
                return false;

            interactionId = localGrant.InteractionId;
            localGrant = null;
            return true;
        }
    }

    public void ClearLocal(string partyId)
    {
        lock (sync)
        {
            if (localGrant?.PartyId == partyId)
                localGrant = null;
        }
    }

    private sealed class SiegeInteractionGrant
    {
        public string InteractionId { get; }
        public string PartyId { get; }
        public string SettlementId { get; }
        public BesiegerCamp PresentedCamp { get; }

        public SiegeInteractionGrant(
            string interactionId,
            string partyId,
            string settlementId,
            BesiegerCamp presentedCamp)
        {
            InteractionId = interactionId;
            PartyId = partyId;
            SettlementId = settlementId;
            PresentedCamp = presentedCamp;
        }

        public bool Matches(
            string interactionId,
            string partyId,
            string settlementId,
            BesiegerCamp presentedCamp) =>
            InteractionId == interactionId &&
            PartyId == partyId &&
            SettlementId == settlementId &&
            ReferenceEquals(PresentedCamp, presentedCamp);

        public bool Matches(string partyId, string settlementId) =>
            PartyId == partyId &&
            SettlementId == settlementId;
    }
}
