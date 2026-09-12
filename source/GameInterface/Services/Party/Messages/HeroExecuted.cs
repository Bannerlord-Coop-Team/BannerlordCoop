using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Party.Messages;

public readonly struct HeroExecuted : IEvent
{
    public readonly Hero ExecutedHero;
    public readonly Hero Executor;
    public readonly KillCharacterAction.KillCharacterActionDetail Detail;
    public readonly bool IsForced;

    public HeroExecuted(
        Hero executedHero,
        Hero executor,
        KillCharacterAction.KillCharacterActionDetail detail,
        bool isForced)
    {
        ExecutedHero = executedHero;
        Executor = executor;
        Detail = detail;
        IsForced = isForced;
    }
}