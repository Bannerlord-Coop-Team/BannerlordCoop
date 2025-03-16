using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Local event when a clan is created
/// </summary>
/// <param name="Data">Data for clan creation</param>
[ProtoContract]
internal record ClanCreated(Clan Clan) : IEvent
{
    [ProtoMember(1)]
    public Clan Clan { get; } = Clan;
}
