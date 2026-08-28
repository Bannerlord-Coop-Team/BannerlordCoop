using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.CampaignService.Messages;

public readonly struct ClientGameOver : IEvent
{
    public readonly Hero PlayerHero;
    public readonly KillCharacterAction.KillCharacterActionDetail Detail;

    public ClientGameOver(
        Hero playerHero,
        KillCharacterAction.KillCharacterActionDetail detail)
    {
        PlayerHero = playerHero;
        Detail = detail;
    }
}

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