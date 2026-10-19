using Coop.Core.Server.Services.SiegeEvents.Messages;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace Coop.Core.Server.Services.SiegeEvents;

/// <summary>
/// The break in each party already paid troops for, so entering the settlement needs a paid break in
/// and asking again for the same siege is never charged twice.
/// </summary>
internal class BreakInReceipts
{
    private readonly Dictionary<MobileParty, Receipt> receipts = new();

    internal sealed class Receipt
    {
        public SiegeEvent Siege;
        public NetworkBreakInResult Result;
    }

    public bool TryGet(MobileParty party, SiegeEvent siege, out Receipt receipt) =>
        receipts.TryGetValue(party, out receipt) && receipt.Siege == siege;

    public void Set(MobileParty party, Receipt receipt) => receipts[party] = receipt;

    public bool IsPaid(MobileParty party, SiegeEvent siege) =>
        TryGet(party, siege, out var receipt) && receipt.Result.Approved;

    public void Remove(MobileParty party) => receipts.Remove(party);

    public void Clear() => receipts.Clear();
}
