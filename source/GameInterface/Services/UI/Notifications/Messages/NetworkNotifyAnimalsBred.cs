using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyAnimalsBred : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly int NumberBred;

    [ProtoMember(3)]
    public readonly ItemRosterElement BredAnimal;

    public NetworkNotifyAnimalsBred(
        string mobilePartyId,
        int numberBred,
        ItemRosterElement bredAnimal)
    {
        MobilePartyId = mobilePartyId;
        NumberBred = numberBred;
        BredAnimal = bredAnimal;
    }
}
