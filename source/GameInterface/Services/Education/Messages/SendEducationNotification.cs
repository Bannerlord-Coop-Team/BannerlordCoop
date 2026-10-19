using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Education.Messages;

public readonly struct SendEducationNotification : IEvent
{
    public readonly Hero ChildToEducate;
    public readonly int Age;
    public readonly short Education;
    public readonly string ControllerId;

    public SendEducationNotification(
        Hero childToEducate,
        int age,
        short education,
        string controllerId)
    {
        ChildToEducate = childToEducate;
        Age = age;
        Education = education;
        ControllerId = controllerId;
    }
}
