using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for LastTimeStampForActivity.
/// </summary>
public readonly struct LastTimeStampChanged : IEvent
{
    public readonly int LastTimeStampForActivity;
    public readonly Hero Hero;

    public LastTimeStampChanged(int lastTimeStampForActivity, Hero hero)
    {
        LastTimeStampForActivity = lastTimeStampForActivity;
        Hero = hero;
    }
}