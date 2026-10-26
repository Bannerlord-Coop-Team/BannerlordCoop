using Coop.Core.Server.Services.SiegeEvents.Messages;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace Coop.Core.Server.Services.SiegeEvents;

/// <summary>
/// The break in each party already paid troops for, so entering the settlement needs a paid break in
/// and asking again for the same siege is never charged twice. Dropped once the party enters, leaves the encounter or its
/// player disconnects.
/// </summary>
internal interface IBreakInReceipts
{
    bool TryGet(MobileParty party, SiegeEvent siege, out BreakInReceipts.Receipt receipt);
    void Set(MobileParty party, BreakInReceipts.Receipt receipt);
    bool IsPaid(MobileParty party, SiegeEvent siege);
    void Remove(MobileParty party);
    void RemoveAll(NetPeer peer);
    void Clear();
}

/// <inheritdoc cref="IBreakInReceipts"/>
internal class BreakInReceipts : IBreakInReceipts
{
    private readonly Dictionary<MobileParty, Receipt> receipts = new();

    internal sealed class Receipt
    {
        public SiegeEvent Siege;
        public NetPeer Peer;
        public NetworkBreakInResult Result;
    }

    public bool TryGet(MobileParty party, SiegeEvent siege, out Receipt receipt) =>
        receipts.TryGetValue(party, out receipt) && receipt.Siege == siege;

    public void Set(MobileParty party, Receipt receipt) => receipts[party] = receipt;

    public bool IsPaid(MobileParty party, SiegeEvent siege) =>
        TryGet(party, siege, out var receipt) && receipt.Result.Approved;

    public void Remove(MobileParty party) => receipts.Remove(party);

    public void RemoveAll(NetPeer peer)
    {
        foreach (var party in receipts.Where(entry => entry.Value.Peer == peer).Select(entry => entry.Key).ToArray())
            receipts.Remove(party);
    }

    public void Clear() => receipts.Clear();
}
