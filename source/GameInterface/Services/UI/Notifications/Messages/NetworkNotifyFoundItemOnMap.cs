using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyFoundItemOnMap : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly int Count;

    [ProtoMember(3)]
    public readonly TextObject ItemName;

    public NetworkNotifyFoundItemOnMap(string mobilePartyId, int count, TextObject itemName)
    {
        MobilePartyId = mobilePartyId;
        Count = count;
        ItemName = itemName;
    }
}
