using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyStillbornDelivery : IEvent
{
    public readonly CharacterObject MotherCharacter;

    public NotifyStillbornDelivery(CharacterObject motherCharacter)
    {
        MotherCharacter = motherCharacter;
    }
}
