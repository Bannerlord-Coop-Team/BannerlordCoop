using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Raised on the server whenever a clan's kingdom changes, so the new value can be replicated
/// explicitly rather than relying on the AutoSync reference field.
/// </summary>
internal readonly struct ClanKingdomMembershipChanged : IEvent
{
    public readonly Clan Clan;
    public readonly Kingdom Kingdom;

    public ClanKingdomMembershipChanged(Clan clan, Kingdom kingdom)
    {
        Clan = clan;
        Kingdom = kingdom;
    }
}

/// <summary>
/// The authoritative kingdom a clan now belongs to. A null <see cref="KingdomId"/> means "no kingdom".
/// </summary>
/// <remarks>
/// AutoSync cannot express this. Its reference-field templates key on the object id
/// (<c>if (!objectManager.TryGetId(data.Value, out string id)) return;</c>), and a null reference has
/// no id - so the set is never sent, and on the receiving side <c>TryGetObject</c> fails so it is
/// never applied. Assigning a synced reference field to null is therefore silently dropped.
///
/// For kingdom membership that meant a clan which left, was expelled, or rebelled stayed a member on
/// every client until the next reload. The join direction always worked, which is why it went
/// unnoticed. This message carries the value either way, so it is correct rather than merely
/// covering the null case.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkClanKingdomMembershipChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string KingdomId;

    public NetworkClanKingdomMembershipChanged(string clanId, string kingdomId)
    {
        ClanId = clanId;
        KingdomId = kingdomId;
    }
}
