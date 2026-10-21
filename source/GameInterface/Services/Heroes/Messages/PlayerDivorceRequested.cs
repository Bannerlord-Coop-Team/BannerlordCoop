using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>Requests divorce for the marriage shown in the client's confirmation.</summary>
public readonly struct PlayerDivorceRequested : IEvent
{
    public readonly Hero PlayerHero;
    public readonly Hero Spouse;

    public PlayerDivorceRequested(Hero playerHero, Hero spouse)
    {
        PlayerHero = playerHero;
        Spouse = spouse;
    }
}

/// <summary>Asks the server to end the requesting player's specified marriage.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestPlayerDivorce : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;
    [ProtoMember(2)]
    public readonly string SpouseId;

    public RequestPlayerDivorce(string playerHeroId, string spouseId)
    {
        PlayerHeroId = playerHeroId;
        SpouseId = spouseId;
    }
}
