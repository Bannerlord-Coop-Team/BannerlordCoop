using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Local event when a clan is destroyed
/// </summary>
internal record ClanDestroyed(Clan Clan, int Details) : IEvent
{
    public Clan Clan { get; } = Clan;
    public int Details { get; } = Details;
}