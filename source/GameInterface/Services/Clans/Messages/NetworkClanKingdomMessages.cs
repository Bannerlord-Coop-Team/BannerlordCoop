using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

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