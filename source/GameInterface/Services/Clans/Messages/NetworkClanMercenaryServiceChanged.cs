using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClanMercenaryServiceChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly bool IsUnderMercenaryService;

    public NetworkClanMercenaryServiceChanged(
        string clanId,
        bool isUnderMercenaryService)
    {
        ClanId = clanId;
        IsUnderMercenaryService = isUnderMercenaryService;
    }
}
