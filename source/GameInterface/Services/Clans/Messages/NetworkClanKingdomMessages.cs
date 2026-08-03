using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClanEntersKingdom : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public NetworkClanEntersKingdom(string clanId)
    {
        ClanId = clanId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClanLeavesKingdom : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public NetworkClanLeavesKingdom(string clanId)
    {
        ClanId = clanId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateBannerColorsOfClan : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public NetworkUpdateBannerColorsOfClan(string clanId)
    {
        ClanId = clanId;
    }
}