using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.UI.Cutscenes.Messages;

public readonly struct InitiateCutscenePlayerCharacterDied : IEvent
{
    public readonly Hero Victim;
    public readonly Hero Killer;
    public readonly KillCharacterAction.KillCharacterActionDetail Detail;

    public InitiateCutscenePlayerCharacterDied(
        Hero victim,
        Hero killer,
        KillCharacterAction.KillCharacterActionDetail detail)
    {
        Victim = victim;
        Killer = killer;
        Detail = detail;
    }
}
