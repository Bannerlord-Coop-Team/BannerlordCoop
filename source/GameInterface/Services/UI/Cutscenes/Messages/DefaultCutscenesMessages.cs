using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

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

public readonly struct InitiateCutsceneHeroComesOfAge : IEvent
{
    public readonly Hero Hero;

    public InitiateCutsceneHeroComesOfAge(Hero hero)
    {
        Hero = hero;
    }
}

internal readonly struct SceneNotificationQueued : IEvent
{
    public SceneNotificationData Notification { get; }

    public SceneNotificationQueued(SceneNotificationData notification)
    {
        Notification = notification;
    }
}

internal readonly struct SceneNotificationClosed : IEvent
{
    public SceneNotificationData Notification { get; }

    public SceneNotificationClosed(SceneNotificationData notification)
    {
        Notification = notification;
    }
}
