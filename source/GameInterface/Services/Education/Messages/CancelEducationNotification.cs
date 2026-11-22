using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Education.Messages;

public readonly struct CancelEducationNotification : IEvent
{
    public readonly Hero ChildToEducate;
    public readonly int Age;
    public readonly string ControllerId;

    public CancelEducationNotification(
        Hero childToEducate,
        int age,
        string controllerId)
    {
        ChildToEducate = childToEducate;
        Age = age;
        ControllerId = controllerId;
    }
}
