using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CampaignService.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClientGameOver : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    public NetworkClientGameOver(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}
