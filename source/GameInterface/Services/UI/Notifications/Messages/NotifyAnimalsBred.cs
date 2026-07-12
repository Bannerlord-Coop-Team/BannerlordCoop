using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyAnimalsBred : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly int NumberBred;
    public readonly ItemRosterElement BredAnimal;

    public NotifyAnimalsBred(
        MobileParty mobileParty,
        int numberBred,
        ItemRosterElement bredAnimal)
    {
        MobileParty = mobileParty;
        NumberBred = numberBred;
        BredAnimal = bredAnimal;
    }
}
